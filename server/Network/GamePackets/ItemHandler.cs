using Revora.Client;
using Revora.Game;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Revora.Network.GamePackets
{
    public static class ItemHandler
    {
        private static bool SocketIs(Game.Enums.Gem socket, Game.Enums.Gem allowed)
        {
            return socket == Game.Enums.Gem.NoSocket || socket == Game.Enums.Gem.EmptySocket || socket == allowed;
        }

        private static bool WingGemOk(Game.Enums.Gem socket)
        {
            return socket == Game.Enums.Gem.NoSocket || socket == Game.Enums.Gem.EmptySocket
                || socket == Game.Enums.Gem.SuperThunderGem || socket == Game.Enums.Gem.SuperGloryGem;
        }

        private static bool IsLegalRevoraTalisman(ConquerItem item)
        {
            if (item == null) return false;
            ushort pos = ItemPosition(item.ID);
            if (pos != ConquerItem.Fan && pos != ConquerItem.Tower && pos != ConquerItem.Wing
                && pos != ConquerItem.SteedCrop && pos != ConquerItem.Steed)
                return false;
            if (item.Enchant > 0) return false;
            if (item.Bless > 7) return false;
            if (pos == ConquerItem.Steed || pos == ConquerItem.SteedCrop)
                return SocketIs(item.SocketOne, Game.Enums.Gem.NoSocket) && SocketIs(item.SocketTwo, Game.Enums.Gem.NoSocket);
            if (pos == ConquerItem.Fan)
                return SocketIs(item.SocketOne, Game.Enums.Gem.SuperThunderGem) && SocketIs(item.SocketTwo, Game.Enums.Gem.SuperThunderGem);
            if (pos == ConquerItem.Tower)
                return SocketIs(item.SocketOne, Game.Enums.Gem.SuperGloryGem) && SocketIs(item.SocketTwo, Game.Enums.Gem.SuperGloryGem);
            if (pos == ConquerItem.Wing)
                return WingGemOk(item.SocketOne) && WingGemOk(item.SocketTwo);
            return false;
        }

        public static void EquipItem(Client.GameState client, uint UID, byte pos)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(UID, out item))
            {
                item.Position = pos;
                client.Inventory.Remove(item, Game.Enums.ItemUse.Move, true);
                client.Equipment.Add(item);
                Database.ConquerItemTable.UpdateLocation(item, client);
            }
        }

        public static void UnEquipItem(Client.GameState client, uint UID)
        {
            var item = client.Equipment.TryGetItem((byte)UID);
            if (item != null)
            {
                if (client.Equipment.Remove((byte)UID))
                {
                    if (item != null)
                    {
                        item.Position = 0;
                    }
                }
                Database.ConquerItemTable.UpdateLocation(item, client);
            }
        }
        public static void StabilazeRefinery(ItemAddingStabilization request, GameState client)
        {
            if (request != null) Revora.Game.Features.RefineryProgress.Stabilize(client, request.ToArray());
        }
        public static void StabilazeArtifact(ItemAddingStabilization stabilizate, GameState client)
        {
            if (stabilizate != null) Revora.Game.Features.ArtifactStabilization.Apply(client, stabilizate);
        }
        public static byte[] WindowStats(Client.GameState client)
        {
            Byte[] Data = new Byte[148];
            Writer.WriteUInt16((UInt16)(Data.Length - 8), 0, Data);
            Writer.WriteUInt16((UInt16)1040, 2, Data);
            Writer.WriteUInt32(client.Entity.UID, 8, Data);
            Writer.WriteUInt32(client.Entity.MaxHitpoints, 12, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MaxMana, 16, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MinAttack, 24, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MaxAttack, 20, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Defence, 28, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MagicAttack, 32, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MagicDefence, 36, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Dodge, 40, Data);
            Writer.WriteUInt32((UInt32)client.AgilityItem, 44, Data);
            Writer.WriteUInt32((UInt32)client.Accuracy, 48, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Gems[1], 52, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Gems[0], 56, Data);
            Writer.WriteUInt32((UInt32)client.MagicDefence, 60, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Gems[7], 64, Data);
            Writer.WriteUInt32((UInt32)client.Entity.ItemBless, 68, Data);
            // Writer.WriteUInt32((UInt32)((1 - client.Entity.ItemBless) * 100), 68, Data);
            Writer.WriteUInt32((UInt32)client.Entity.CriticalStrike, 72, Data);
            Writer.WriteUInt32((UInt32)client.Entity.SkillCStrike, 76, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Immunity, 80, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Penetration, 84, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Block, 88, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Breaktrough, 92, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Counteraction, 96, Data);
            Writer.WriteUInt32((UInt32)client.Entity.Detoxication, 100, Data);
            Writer.WriteUInt32((UInt32)client.Entity.PhysicalDamageIncrease, 104, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MagicDamageIncrease, 108, Data);
            Writer.WriteUInt32((UInt32)client.Entity.PhysicalDamageDecrease, 112, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MagicDamageDecrease, 116, Data);
            Writer.WriteUInt32((UInt32)client.Entity.MetalResistance, 120, Data);
            Writer.WriteUInt32((UInt32)client.Entity.WoodResistance, 124, Data);
            Writer.WriteUInt32((UInt32)client.Entity.WaterResistance, 128, Data);
            Writer.WriteUInt32((UInt32)client.Entity.FireResistance, 132, Data);
            Writer.WriteUInt32((UInt32)client.Entity.EarthResistance, 136, Data);
            return Data;
        }
        public static void PurifyItem(Purification ps, Client.GameState client)
        {
            if (ps != null) Revora.Game.Features.ArtifactApplication.Apply(client, ps);
        }
        public static void PurifyRefinery(Purification request, GameState client)
        {
            if (request != null) Revora.Game.Features.RefineryProgress.Apply(client, request.ToArray());
        }
        public static bool IsEquipment(long ID)
        {
            return ItemPosition((uint)ID) != 0;
        }
        public static void ReloadArrows(ConquerItem arrow, GameState client)
        {
            if (client.Entity.Class < 40 || client.Entity.Class > 45)
            {
                return;
            }

            if (client.Equipment.Free(4))
            {
                return;
            }

            if (client.Equipment.TryGetItem(4).ID / 1000 != 500)
            {
                return;
            }

            client.Equipment.DestroyArrow(5);
            uint id = 1050002;
            if (arrow != null)
            {
                id = arrow.ID;
            }

            if (client.Inventory.Contains(id, 1))
            {
                var newArrow = client.Inventory.GetItemByID(id);
                newArrow.Position = 5;
                client.Inventory.Remove(newArrow, Game.Enums.ItemUse.Move);
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, 0);

                client.Equipment.Add(newArrow);
                client.Equipment.Objects[4].UID += 0;
                client.Equipment.Objects[4].Send(client);

                ClientEquip equips = new ClientEquip();
                equips.DoEquips(client);
                client.Send(equips);

                client.Send(Constants.ArrowsReloaded);
            }
            else if (!client.Inventory.Contains(id, 1))
            {
                client.Send(Constants.NoArrows(Database.ConquerItemInformation.BaseInformations[id].Name));
            }
        }
        public static void ComposePlus(Compose compose, Client.GameState client)
        {
            if(client==null || client.ItemSyncRoot==null || compose==null)return;
            lock(client.ItemSyncRoot)
            {
                if(Revora.Game.Features.SteedBreeding.Handle(client,compose.ToArray()))return;
                if(Revora.Game.Features.EquipmentUpgrades.HandleCompose(client,compose.ToArray()))return;
                ComposePlusLegacy(compose,client);
            }
        }
        private static void ComposePlusLegacy(Compose compose, Client.GameState client)
        {
            ConquerItem Item = null;
            ConquerItem[] Minors = null;
            if (client.Inventory.TryGetItem(compose[0], out Item) || (client.Equipment.TryGetItem(compose[0]) != null))
            {
                if (Item == null && client.Inventory.TryGetItem(compose[0], out Item) == false &&
                   client.Equipment.TryGetItem(compose[0]) != null)
                {
                    Item = client.Equipment.TryGetItem(compose[0]);
                }

                if (compose.Mode == Compose.DragonBallUpgrade || compose.Mode == Compose.MeteorUpgrade)
                {
                    Minors = new ConquerItem[compose.Countx];
                    for (int i = 0; i < compose.Countx; i++)
                    {
                        if (!client.Inventory.TryGetItem(compose[i + 2], out Minors[i]))
                        {
                            return;
                        }
                        else if (Minors[i].UID == Item.UID)
                        {
                            return;
                        }
                    }
                }
                else
                {
                    Minors = new ConquerItem[compose.Count - 1];
                    for (int i = 0; i < compose.Count - 1; i++)
                    {
                        if (!client.Inventory.TryGetItem(compose[i + 1], out Minors[i]))
                        {
                            return;
                        }
                        else if (Minors[i].UID == Item.UID)
                        {
                            return;
                        }
                    }
                }
                ushort pos = ItemPosition(Item.ID);
                if (pos == ConquerItem.Bottle || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory ||
                    pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.SteedArmor) //mohsen.. i'm remove crop
                {
                    return;
                }

                if (compose.Mode == Compose.ChanceUpgrade)
                {
                    if (Item.Plus < 12 && Item.PlusProgress != 0)
                    {
                        double percent = (double)Item.PlusProgress /
                                         (double)Database.DataHolder.ComposePlusPoints(Item.Plus);
                        if (Kernel.Rate(percent))
                        {
                            Item.PlusProgress = 0;
                            Item.Plus++;
                        }
                        else
                        {
                            Item.PlusProgress = 0;
                        }

                        Item.Mode = Revora.Game.Enums.ItemMode.Update;
                        Item.Send(client);
                        Database.ConquerItemTable.UpdatePlus(Item);
                        Database.ConquerItemTable.UpdatePlusProgress(Item);
                        if (Item.Plus >= 9)
                        {
                            Kernel.SendWorldMessage(
                                new Message(
                                    "Congratulations! " + client.Entity.Name + " has upgraded " +
                                    Database.ConquerItemInformation.BaseInformations[Item.ID].Name + " to +" +
                                    Item.Plus + " with " + Item.PlusProgress + " progress!", System.Drawing.Color.Red,
                                    Network.GamePackets.Message.TopLeft), Server.GamePool);
                        }
                    }
                    return;
                }
                //bool done = false;
                foreach (var item in Minors)
                {
                    var ItemPlus = item;
                    Database.ConquerItemInformation infos =
                        new Revora.Database.ConquerItemInformation(Item.ID, Item.Plus);
                    switch (compose.Mode)
                    {
                        case Compose.DragonBallUpgrade:
                            {
                                if (compose.Mode == Compose.DragonBallUpgrade)
                                {
                                    Item = client.GetItem(compose[0]);
                                    if (Item != null)
                                    {
                                        pos = ItemPosition(Item.ID);
                                        if (pos == ConquerItem.Bottle || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.SteedArmor) //mohsen.. i'm remove crop
                                        {
                                            return;
                                        }

                                        Minors = new ConquerItem[compose.Count - 2];
                                        for (int i = 0; i < compose.Count - 2; i++)
                                        {
                                            if (!client.Inventory.TryGetItem(compose.GetItem(i + 1), out Minors[i]))
                                            {
                                                return;
                                            }
                                            else
                                                if (Minors[i].UID == Item.UID)
                                            {
                                                return;
                                            }
                                        }

                                        byte counts_itsm = (byte)Minors.Length;
                                        if (Item.ID % 10 < 9)
                                        {
                                            var itemdetail = Revora.Database.ConquerItemInformation.BaseInformations[Item.ID];
                                            byte cost = (byte)(itemdetail.Level / 30);
                                            cost += (byte)((Item.ID % 10) / 2);

                                            if (Minors.Length < cost)
                                            {
                                                byte rand = (byte)Kernel.Random.Next(1, 100);
                                                if (rand < 45)
                                                {
                                                    if (Item.ID % 10 < 5)
                                                    {
                                                        Item.ID += 5 - Item.ID % 10;
                                                    }

                                                    Item.ID++;
                                                    Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                    iu.UID = Item.UID;
                                                    iu.dwParam = 1;
                                                    iu.Action = Network.GamePackets.ItemUsage.Mode.DragonBallUpgrade;
                                                    client.Send(iu);

                                                    Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                    Database.ConquerItemTable.UpdateItemID(Item, client);
                                                    Item.Send(client);
                                                    client.LoadItemStats();
                                                    client.Equipment.UpdateEntityPacket();
                                                }
                                                else
                                                {
                                                    Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                    iu.UID = Item.UID;
                                                    iu.dwParam = 2;
                                                    iu.Action = Network.GamePackets.ItemUsage.Mode.DragonBallUpgrade;
                                                    client.Send(iu);

                                                    Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                    Database.ConquerItemTable.UpdateItemID(Item, client);
                                                    Item.Send(client);
                                                    client.LoadItemStats();
                                                    client.Equipment.UpdateEntityPacket();
                                                }

                                            }
                                            else
                                            {
                                                if (Item.ID % 10 < 5)
                                                {
                                                    Item.ID += 5 - Item.ID % 10;
                                                }

                                                Item.ID++;

                                                Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                iu.UID = Item.UID;
                                                iu.dwParam = 1;
                                                iu.Action = Network.GamePackets.ItemUsage.Mode.DragonBallUpgrade;
                                                client.Send(iu);

                                                Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                Database.ConquerItemTable.UpdateItemID(Item, client);
                                                Item.Send(client);
                                                client.LoadItemStats();
                                                client.Equipment.UpdateEntityPacket();
                                            }

                                            foreach (var Ite in Minors)
                                            {
                                                client.Inventory.Remove(Ite, Enums.ItemUse.Remove);
                                            }
                                        }

                                    }
                                }
                                break;
                            }
                        case Compose.MeteorUpgrade:
                            {
                                if (compose.Mode == Compose.MeteorUpgrade)
                                {
                                    Item = client.GetItem(compose[0]);
                                    if (Item != null)
                                    {
                                        pos = ItemPosition(Item.ID);
                                        if (pos == ConquerItem.Bottle || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.SteedArmor) //mohsen.. i'm remove crop
                                        {
                                            return;
                                        }

                                        Minors = new ConquerItem[compose.Count - 2];
                                        for (int i = 0; i < compose.Count - 2; i++)
                                        {
                                            if (!client.Inventory.TryGetItem(compose.GetItem(i + 1), out Minors[i]))
                                            {
                                                return;
                                            }
                                            else
                                                if (Minors[i].UID == Item.UID)
                                            {
                                                return;
                                            }
                                        }
                                        byte counts_itsm = (byte)Minors.Length;

                                        infos = new Revora.Database.ConquerItemInformation(Item.ID, Item.Plus);


                                        if (Minors[0].ID == 1088000)
                                        {
                                            Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                            iu.UID = Item.UID;
                                            iu.dwParam = 1;
                                            iu.Action = Network.GamePackets.ItemUsage.Mode.MeteorUpgrade;
                                            client.Send(iu);

                                            Item.ID = infos.CalculateUplevel();

                                            Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                            Database.ConquerItemTable.UpdateItemID(Item, client);
                                            Item.Send(client);
                                            client.LoadItemStats();
                                            client.Equipment.UpdateEntityPacket();



                                        }
                                        else
                                        {
                                            byte cost = GetUpdateItem(infos.BaseInformation);// (byte)(infos.BaseInformation.Level / 6);
                                            //  cost += (byte)((Item.ID % 10) / 3);
                                            if (cost <= Minors.Length)
                                            {
                                                Item.ID = infos.CalculateUplevel();
                                                Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                iu.UID = Item.UID;
                                                iu.dwParam = 1;
                                                iu.Action = Network.GamePackets.ItemUsage.Mode.MeteorUpgrade;
                                                client.Send(iu);

                                                Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                Database.ConquerItemTable.UpdateItemID(Item, client);
                                                Item.Send(client);

                                                client.LoadItemStats();
                                                client.Equipment.UpdateEntityPacket();
                                            }
                                            else
                                            {
                                                byte rand = (byte)Kernel.Random.Next(1, 100);
                                                if (rand < 40)
                                                {
                                                    Item.ID = infos.CalculateUplevel();
                                                    Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                    iu.UID = Item.UID;
                                                    iu.Action = Network.GamePackets.ItemUsage.Mode.MeteorUpgrade;
                                                    iu.dwParam = 1;
                                                    client.Send(iu);
                                                    Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                    Database.ConquerItemTable.UpdateItemID(Item, client);
                                                    Item.Send(client);
                                                    client.LoadItemStats();
                                                    client.Equipment.UpdateEntityPacket();
                                                }
                                                else
                                                {
                                                    Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                                                    iu.UID = Item.UID;
                                                    iu.Action = Network.GamePackets.ItemUsage.Mode.MeteorUpgrade;
                                                    iu.dwParam = 2;
                                                    client.Send(iu);
                                                    Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                                    Database.ConquerItemTable.UpdateItemID(Item, client);
                                                    Item.Send(client);
                                                    client.LoadItemStats();
                                                    client.Equipment.UpdateEntityPacket();
                                                }
                                            }
                                        }
                                        foreach (var Ite in Minors)
                                        {
                                            client.Inventory.Remove(Ite, Enums.ItemUse.Remove);
                                        }
                                    }

                                }
                                break;
                            }
                        case Compose.Plus:
                            {
                                if (Item.Plus < 12 && Item.GetItemType() != ConquerItem.ItemTypes.GarmentID_1 &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.GarmentID_2 &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.BottleID &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.RightAccessory1 &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.RightAccessory2 &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.RightAccessory3 &&
                                    Item.GetItemType() != ConquerItem.ItemTypes.LeftAccessory)
                                {
                                    Item.PlusProgress += Database.DataHolder.StonePlusPoints(ItemPlus.Plus);
                                    while (Item.PlusProgress >= Database.DataHolder.ComposePlusPoints(Item.Plus) &&
                                           Item.Plus != 12)
                                    {
                                        Item.PlusProgress -= Database.DataHolder.ComposePlusPoints(Item.Plus);
                                        Item.Plus++;
                                        if (Item.Plus == 12)
                                        {
                                            Item.PlusProgress = 0;
                                        }
                                    }
                                    Item.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Item.Send(client);
                                    Database.ConquerItemTable.UpdatePlus(Item);
                                    Database.ConquerItemTable.UpdatePlusProgress(Item);
                                    client.Inventory.Remove(ItemPlus, Revora.Game.Enums.ItemUse.Remove);
                                    if (Item.Plus >= 9)
                                    {
                                        Kernel.SendWorldMessage(
                                            new Message(
                                                "Congratulations! " + client.Entity.Name + " has upgraded " +
                                                Database.ConquerItemInformation.BaseInformations[Item.ID].Name + " to +" +
                                                Item.Plus + " with " + Item.PlusProgress + " progress!",
                                                System.Drawing.Color.Red, Network.GamePackets.Message.TopLeft),
                                            Server.GamePool);
                                    }
                                }
                                break;
                            }

                    }
                }
            }
        }
        static byte GetUpdateItem(Database.ConquerItemBaseInformation item)
        {
            byte cost = 0;
            byte Quality = (byte)(item.ID % 10);
            if (item.Level < 70)
            {
                if (Quality < 7)
                {
                    cost += 2;
                }
                else if (Quality == 7)
                {
                    cost += 2;
                }
                else if (Quality == 8)
                {
                    cost += 3;
                }
                else if (Quality == 9)
                {
                    cost += 3;
                }
            }
            else
            {
                if (Quality < 7)
                {
                    cost += 2;
                }
                else if (Quality == 7)
                {
                    cost += 2;
                }
                else if (Quality == 8)
                {
                    cost += 5;
                }
                else if (Quality == 9)
                {
                    cost += (byte)(item.Level / 4);
                }
            }
            return cost;
        }
        public static byte GetLevel(uint ID)
        {
            if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
            {
                return (byte)((ID % 100) / 10);
            }
            else
            {
                return (byte)((ID % 1000) / 10);
            }
        }
        public static byte MeteorUpgradeCost(uint ID)
        {
            int wQuality = (int)(ID % 10);
            byte nLevel = GetLevel(ID);
            switch (wQuality)
            {
                case 9:
                    {
                        if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
                        {
                            if (nLevel <= 4)
                            {
                                return 3;
                            }

                            if (nLevel == 5)
                            {
                                return 25;
                            }

                            if (nLevel == 6)
                            {
                                return 31;
                            }

                            if (nLevel == 7)
                            {
                                return 40;
                            }

                            if (nLevel > 7)
                            {
                                return 61;
                            }
                        }
                        else
                        {
                            if (nLevel <= 10)
                            {
                                return 3;
                            }
                            else if (nLevel <= 12)
                            {
                                return 14;
                            }
                            else if (nLevel <= 14)
                            {
                                return 15;
                            }
                            else if (nLevel <= 16)
                            {
                                return 18;
                            }
                            else if (nLevel <= 18)
                            {
                                return 20;
                            }
                            else if (nLevel <= 20)
                            {
                                return 25;
                            }
                            else
                            {
                                return 31;
                            }
                        }
                        break;
                    }
                case 8:
                    {
                        if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
                        {
                            if (nLevel <= 4)
                            {
                                return 2;
                            }

                            if (nLevel == 5)
                            {
                                return 8;
                            }

                            if (nLevel == 6)
                            {
                                return 10;
                            }

                            if (nLevel == 7)
                            {
                                return 14;
                            }

                            if (nLevel > 7)
                            {
                                return 20;
                            }
                        }
                        else
                        {
                            if (nLevel <= 10)
                            {
                                return 2;
                            }
                            else if (nLevel <= 12)
                            {
                                return 4;
                            }
                            else if (nLevel <= 16)
                            {
                                return 6;
                            }
                            else if (nLevel <= 18)
                            {
                                return 7;
                            }
                            else if (nLevel <= 20)
                            {
                                return 8;
                            }
                            else
                            {
                                return 9;
                            }
                        }
                        break;
                    }
                case 7:
                    {
                        if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
                        {
                            if (nLevel <= 4)
                            {
                                return 2;
                            }

                            if (nLevel > 4 && nLevel < 6)
                            {
                                return 3;
                            }

                            if (nLevel == 6)
                            {
                                return 4;
                            }

                            if (nLevel == 7)
                            {
                                return 6;
                            }

                            if (nLevel > 7)
                            {
                                return 9;
                            }
                        }
                        else
                        {
                            if (nLevel <= 15)
                            {
                                return 2;
                            }
                            else if (nLevel <= 20)
                            {
                                return 3;
                            }
                            else
                            {
                                return 4;
                            }
                        }
                        break;
                    }
                case 6:
                    {
                        if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
                        {
                            if (nLevel <= 4)
                            {
                                return 2;
                            }

                            if (nLevel > 4 && nLevel < 7)
                            {
                                return 3;
                            }

                            if (nLevel == 7)
                            {
                                return 4;
                            }

                            if (nLevel > 7)
                            {
                                return 7;
                            }
                        }
                        else
                        {
                            if (nLevel <= 18)
                            {
                                return 2;
                            }
                            else
                            {
                                return 3;
                            }
                        }
                        break;
                    }
                default:
                    {
                        if (ItemPosition(ID) == (ushort)ConquerItem.Armor || ItemPosition(ID) == (ushort)ConquerItem.Head || IsShield(ID))
                        {
                            if (nLevel <= 4)
                            {
                                return 2;
                            }

                            if (nLevel > 4 && nLevel < 7)
                            {
                                return 3;
                            }

                            if (nLevel == 7)
                            {
                                return 4;
                            }

                            if (nLevel > 7)
                            {
                                return 7;
                            }
                        }
                        else
                        {
                            if (nLevel <= 19)
                            {
                                return 2;
                            }
                            else
                            {
                                return 3;
                            }
                        }
                        break;
                    }
            }
            return 0;
        }
        public static byte DragonBallUpgradeCost(ConquerItem Item, Database.ConquerItemInformation info)
        {
            var level = info.BaseInformation.Level;
            byte needed = 0;
            if (Item.ID % 10 >= (uint)Enums.ItemQuality.Normal && Item.ID % 10 <= (uint)Enums.ItemQuality.NormalV3)
            {
                needed = 1;
                if (level > 70 && level <= 120)
                {
                    needed += 1;
                }
                else if (level > 120 && level <= 136)
                {
                    needed += 2;
                }
                else if (level > 136 && level <= 140)
                {
                    needed += 3;
                }
            }
            else if (Item.ID % 10 == (uint)Enums.ItemQuality.Refined)
            {
                needed = 2;
                if (level > 70 && level <= 100)
                {
                    needed += 1;
                }
                else if (level > 100 && level <= 120)
                {
                    needed += 2;
                }
                else if (level > 120 && level <= 130)
                {
                    needed += 3;
                }
                else if (level > 130 && level <= 136)
                {
                    needed += 4;
                }
                else if (level > 136 && level <= 140)
                {
                    needed += 5;
                }
            }
            else if (Item.ID % 10 == (uint)Enums.ItemQuality.Unique)
            {
                needed = 4;
                if (level > 90 && level <= 105)
                {
                    needed += 1;
                }
                else if (level > 105 && level <= 120)
                {
                    needed += 2;
                }
                else if (level > 120 && level <= 125)
                {
                    needed += 3;
                }
                else if (level > 125 && level <= 130)
                {
                    needed += 4;
                }
                else if (level > 130 && level <= 133)
                {
                    needed += 5;
                }
                else if (level > 133 && level <= 139)
                {
                    needed += 6;
                }
                else if (level == 140)
                {
                    needed += 8;
                }
            }
            else if (Item.ID % 10 == (uint)Enums.ItemQuality.Elite)
            {
                needed = 5;
                if (level > 70 && level <= 85)
                {
                    needed += 1;
                }
                else if (level > 85 && level <= 95)
                {
                    needed += 2;
                }
                else if (level > 95 && level <= 105)
                {
                    needed += 3;
                }
                else if (level > 105 && level <= 110)
                {
                    needed += 4;
                }
                else if (level > 110 && level <= 120)
                {
                    needed += 5;
                }
                else if (level > 120 && level <= 125)
                {
                    needed += 7;
                }
                else if (level > 125 && level <= 130)
                {
                    needed += 8;
                }
                else if (level > 130 && level <= 135)
                {
                    needed += 10;
                }
                else if (level > 135 && level <= 140)
                {
                    needed += 12;
                }
            }
            return needed;
        }
        //static void SocketItem(ItemUsage usage, Client.GameState client)
        //{
        //    ConquerItem item = null;
        //    ConquerItem[] Minors = null;
        //    if (client.Inventory.TryGetItem(usage.UID, out item))
        //    {
        //        ushort pos = ItemPosition(item.ID);
        //        if (pos == ConquerItem.Bottle || pos == ConquerItem.Fan || pos == ConquerItem.Garment || pos == ConquerItem.LeftWeaponAccessory || pos == ConquerItem.RightWeaponAccessory || pos == ConquerItem.Steed || pos == ConquerItem.SteedArmor || pos == ConquerItem.SteedCrop || pos == ConquerItem.Tower || pos == ConquerItem.Wing)
        //        {
        //            client.Send(new Message("Sorry can't make socket in this item !", System.Drawing.Color.Red, Message.Talk));
        //            return;
        //        }

        //        Minors = new ConquerItem[usage.Batch.Length];
        //        for (int i = 0; i < usage.Batch.Length; i++)
        //        {
        //            if (!client.Inventory.TryGetItem(usage.Batch[i], out Minors[i]))
        //                return;
        //            else
        //                if (Minors[i].UID == item.UID)
        //                    return;
        //        }
        //        if (Minors.Length == 0)
        //            return;
        //        if (item.SocketOne == Revora.Game.Enums.Gem.NoSocket)
        //        {
        //            ushort cost = 1;
        //            if (pos != ConquerItem.RightWeapon && pos != ConquerItem.LeftWeapon)
        //                cost = 12;

        //            if (client.Inventory.Contains(1088000, cost))
        //            {
        //                item.SocketOne = Revora.Game.Enums.Gem.EmptySocket;
        //                item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                item.Send(client);
        //                usage.dwParam = 1;
        //                client.LoadItemStats();
        //                Database.ConquerItemTable.UpdateSockets(item);
        //                foreach (var xitem in Minors)
        //                {
        //                    client.Inventory.Remove(xitem.UID, Revora.Game.Enums.ItemUse.Remove, true);
        //                }
        //            }
        //        }
        //        else if (item.SocketTwo == Revora.Game.Enums.Gem.NoSocket)
        //        {
        //            ushort cost = 5;
        //            if (pos != ConquerItem.RightWeapon && pos != ConquerItem.LeftWeapon)
        //            {
        //                cost = 1;
        //                var minor = Minors.FirstOrDefault();
        //                if (minor.ID == 1200005)
        //                {
        //                    if (client.Inventory.Contains(1200005, cost))
        //                    {
        //                        if (Kernel.Rate((double)30))
        //                        {
        //                            item.SocketTwo = Revora.Game.Enums.Gem.EmptySocket;
        //                            item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                            item.Send(client);
        //                            usage.dwParam = 1;
        //                            client.LoadItemStats();
        //                            Database.ConquerItemTable.UpdateSockets(item);
        //                        }
        //                        else
        //                        {
        //                            usage.dwParam = 0;
        //                            client.Inventory.Add(1200006, 0, 1);
        //                        }
        //                        foreach (var xitem in Minors)
        //                        {
        //                            client.Inventory.Remove(xitem.UID, Revora.Game.Enums.ItemUse.Remove, true);
        //                        }
        //                    }
        //                }
        //                else if (minor.ID == 1200006)
        //                {
        //                    cost = 7;
        //                    if (client.Inventory.Contains(1200006, cost))
        //                    {
        //                        item.SocketTwo = Revora.Game.Enums.Gem.EmptySocket;
        //                        item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                        item.Send(client);
        //                        usage.dwParam = 1;
        //                        client.LoadItemStats();
        //                        Database.ConquerItemTable.UpdateSockets(item);
        //                        foreach (var xitem in Minors)
        //                        {
        //                            client.Inventory.Remove(xitem.UID, Revora.Game.Enums.ItemUse.Remove, true);
        //                        }
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                if (client.Inventory.Contains(1088000, cost))
        //                {
        //                    item.SocketTwo = Revora.Game.Enums.Gem.EmptySocket;
        //                    item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                    item.Send(client);
        //                    usage.dwParam = 1;
        //                    client.LoadItemStats();
        //                    Database.ConquerItemTable.UpdateSockets(item);
        //                    foreach (var xitem in Minors)
        //                    {
        //                        client.Inventory.Remove(xitem.UID, Revora.Game.Enums.ItemUse.Remove, true);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        public static void SocketItem(EmbedSocket socket, Client.GameState client)
        {
            Revora.Game.Features.EquipmentSockets.Embed(client,socket);
        }
        //public static void SocketItem(EmbedSocket socket, Client.GameState client) -> ORIGINAL
        //{
        //    ConquerItem Item = null;
        //    ConquerItem Gem = null;
        //    if (client.Inventory.TryGetItem(socket.ItemUID, out Item) || client.Equipment.TryGetItem(socket.ItemUID) != null)
        //    {
        //        if (client.Equipment.TryGetItem(socket.ItemUID) != null)
        //            Item = client.Equipment.TryGetItem(socket.ItemUID);
        //        ushort sock = ItemPosition(Item.ID);
        //        if (sock == 7 || sock == 9 || sock > 11 && sock > 19)
        //        {
        //            client.Send(new Message("Lo sentimos, no puede hacer que toma en este art�culo!", System.Drawing.Color.Red, Message.Talk));
        //            return;
        //        }
        //        if (client.Inventory.TryGetItem(socket.GemUID, out Gem) || socket.Mode == EmbedSocket.Remove)
        //        {
        //            switch (socket.Mode)
        //            {
        //                case EmbedSocket.Add:
        //                    {
        //                        byte gemBase = (byte)(Gem.ID % 1000);
        //                        if (Enum.IsDefined(typeof(Game.Enums.Gem), gemBase))
        //                        {
        //                            switch (socket.Slot)
        //                            {
        //                                case EmbedSocket.SlotOne:
        //                                    {
        //                                        if ((byte)Item.SocketOne == 255)
        //                                        {
        //                                            var gem = (Game.Enums.Gem)(Gem.ID % 1000);
        //                                            if ((Item.ID >= 201003 && Item.ID <= 20109) && Item.ID <= 204009 &&
        //                                                gem != Enums.Gem.NormalThunderGem && gem != Enums.Gem.RefinedThunderGem &&
        //                                                gem != Enums.Gem.SuperThunderGem)
        //                                                return;
        //                                            if ((Item.ID >= 202003 && Item.ID <= 202009) &&
        //                                                gem != Enums.Gem.NormalGloryGem && gem != Enums.Gem.RefinedGloryGem &&
        //                                                gem != Enums.Gem.SuperGloryGem)
        //                                                return;
        //                                            Item.SocketOne = gem;
        //                                            Item.Mode = Game.Enums.ItemMode.Update;
        //                                            Item.Send(client);
        //                                            Database.ConquerItemTable.UpdateSockets(Item);
        //                                            client.Inventory.Remove(Gem, Game.Enums.ItemUse.Remove);
        //                                            break;
        //                                        }
        //                                        break;
        //                                    }
        //                                case EmbedSocket.SlotTwo:
        //                                    {
        //                                        var gem = (Game.Enums.Gem)(Gem.ID % 1000);
        //                                        if ((Item.ID >= 201003 && Item.ID <= 20109)  && gem != Enums.Gem.NormalThunderGem &&
        //                                            gem != Enums.Gem.RefinedThunderGem && gem != Enums.Gem.SuperThunderGem)
        //                                            return;
        //                                        if ((Item.ID >= 201003 && Item.ID <= 20109) && Item.ID <= 204009 && gem != Enums.Gem.NormalGloryGem &&
        //                                            gem != Enums.Gem.RefinedGloryGem && gem != Enums.Gem.SuperGloryGem)
        //                                            return;
        //                                        if ((byte)Item.SocketOne > 0 && (byte)Item.SocketOne < 255)
        //                                        {
        //                                            if ((byte)Item.SocketTwo == 255)
        //                                            {
        //                                                Item.SocketTwo = gem;
        //                                                Item.Mode = Game.Enums.ItemMode.Update;
        //                                                Item.Send(client);
        //                                                Database.ConquerItemTable.UpdateSockets(Item);
        //                                                client.Inventory.Remove(Gem, Game.Enums.ItemUse.Remove);
        //                                            }
        //                                        }
        //                                        else if ((byte)Item.SocketOne == 255)
        //                                        {
        //                                            Item.SocketOne = gem;
        //                                            Item.Mode = Game.Enums.ItemMode.Update;
        //                                            Item.Send(client);
        //                                            Database.ConquerItemTable.UpdateSockets(Item);
        //                                            client.Inventory.Remove(Gem, Game.Enums.ItemUse.Remove);
        //                                        }
        //                                        break;
        //                                    }
        //                            }
        //                        }
        //                        break;
        //                    }
        //                case EmbedSocket.Remove:
        //                    {
        //                        switch (socket.Slot)
        //                        {
        //                            case EmbedSocket.SlotOne:
        //                                {
        //                                    if ((byte)Item.SocketOne != 0)
        //                                    {
        //                                        Item.SocketOne = Revora.Game.Enums.Gem.EmptySocket;
        //                                        if ((byte)Item.SocketTwo != 0 && (byte)Item.SocketTwo != 255)
        //                                        {
        //                                            Item.SocketOne = Revora.Game.Enums.Gem.EmptySocket;
        //                                            Item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                                            Item.Send(client);
        //                                            Database.ConquerItemTable.UpdateSockets(Item);
        //                                        }
        //                                        break;
        //                                    }
        //                                    break;
        //                                }
        //                            case EmbedSocket.SlotTwo:
        //                                {
        //                                    if ((byte)Item.SocketTwo != 0 && (byte)Item.SocketTwo != 255)
        //                                    {
        //                                        Item.SocketTwo = Revora.Game.Enums.Gem.EmptySocket;
        //                                        Item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                                        Item.Send(client);
        //                                        Database.ConquerItemTable.UpdateSockets(Item);
        //                                    }
        //                                    else
        //                                    {
        //                                        if ((byte)Item.SocketOne != 0)
        //                                        {
        //                                            Item.SocketOne = Revora.Game.Enums.Gem.EmptySocket;
        //                                            Item.Mode = Revora.Game.Enums.ItemMode.Update;
        //                                            Item.Send(client);
        //                                            Database.ConquerItemTable.UpdateSockets(Item);
        //                                        }
        //                                    }
        //                                    break;
        //                                }
        //                        }
        //                        break;
        //                    }
        //            }
        //        }
        //        client.Send(socket);
        //        System.Threading.Thread.Sleep(500);
        //    }
        //}


        public static void UnlockItemInstant(ItemLock itemLock, GameState client)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemLock.UID, out item))
            {
                if (item.Lock == 1)
                {
                    item.Lock = 0;
                    item.Mode = Game.Enums.ItemMode.Update;
                    item.Send(client);
                    Database.ConquerItemTable.UpdateLock(item);
                }
                else
                {
                    client.Send(new Message("You cannot open an item that is still unlocking.", System.Drawing.Color.FloralWhite, Message.TopLeft));
                }
            }
        }
        public static void SocketTalismanWithItem(ItemUsage itemUsage, Client.GameState client)
        {
            ConquerItem talisman = client.Equipment.TryGetItem(itemUsage.UID);
            if (talisman == null)
            {
                client.Inventory.TryGetItem(itemUsage.UID, out talisman);
            }

            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemUsage.dwParam2, out item))
            {
                if (talisman == null)
                {
                    return;
                }

                if (item.ID / 1000 == talisman.ID / 1000)
                {
                    return;
                }

                if (item.Bound == true)
                {
                    return;
                }

                if (talisman.SocketTwo != Game.Enums.Gem.NoSocket)
                {
                    return;
                }

                ushort Points = 0;
                switch (item.ID % 10)
                {
                    case 6:
                        Points += 5; break;
                    case 7:
                        Points += 10; break;
                    case 8:
                        Points += 40; break;
                    case 9:
                        Points += 1000; break;
                }
                Points += Database.DataHolder.TalismanPlusPoints(item.Plus);
                int position = ItemPosition(item.ID);
                switch (position)
                {
                    case 0:
                        return;
                    case 4:
                    case 5:
                        if (item.ID % 10 >= 8)
                        {
                            if (item.SocketOne != Game.Enums.Gem.NoSocket)
                            {
                                Points += 160;
                            }

                            if (item.SocketTwo != Game.Enums.Gem.NoSocket)
                            {
                                Points += 800;
                            }
                        }
                        break;
                    default:
                        if (item.ID % 10 >= 8)
                        {
                            if (item.SocketOne != Game.Enums.Gem.NoSocket)
                            {
                                Points += 2000;
                            }

                            if (item.SocketTwo != Game.Enums.Gem.NoSocket)
                            {
                                Points += 6000;
                            }
                        }
                        break;
                }
                talisman.SocketProgress += Points;
                if (talisman.SocketOne == Game.Enums.Gem.NoSocket)
                {
                    if (talisman.SocketProgress >= 8000)
                    {
                        talisman.SocketProgress -= 8000;
                        talisman.SocketOne = Game.Enums.Gem.EmptySocket;
                        talisman.Mode = Game.Enums.ItemMode.Update;
                        talisman.Send(client);
                    }
                }
                if (talisman.SocketOne != Game.Enums.Gem.NoSocket)
                {
                    if (talisman.SocketProgress >= 20000)
                    {
                        talisman.SocketProgress = 0;
                        talisman.SocketTwo = Game.Enums.Gem.EmptySocket;
                    }
                }
                Database.ConquerItemTable.UpdateSocketProgress(talisman);
                Database.ConquerItemTable.UpdateSockets(talisman);
                talisman.Mode = Game.Enums.ItemMode.Update;
                talisman.Send(client);
                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
            }
            client.LoadItemStats();
        }
        public static void SocketTalismanWithCPs(ItemUsage itemUsage, Client.GameState client)
        {
            ConquerItem talisman = client.Equipment.TryGetItem(itemUsage.UID);
            if (talisman == null)
            {
                client.Inventory.TryGetItem(itemUsage.UID, out talisman);
            }

            if (talisman == null)
            {
                return;
            }

            ushort pos = ItemPosition(talisman.ID);
            if (pos != ConquerItem.Fan && pos != ConquerItem.Tower && pos != ConquerItem.Wing)
            {
                return;
            }

            uint price = 0;
            if (talisman.SocketOne == Game.Enums.Gem.NoSocket)
            {
                double procent = 100 - (talisman.SocketProgress * 25600 / 2048000);
                if (100 - procent < 25)
                {
                    return;
                }

                price = (uint)(procent * 55);
            }
            else if (talisman.SocketTwo == Game.Enums.Gem.NoSocket)
            {
                double procent = 100 - (talisman.SocketProgress * 25600 / 5120000);
                if (100 - procent < 25)
                {
                    return;
                }

                price = (uint)(procent * 110);
            }
            else
            {
                return;
            }

            if (client.Entity.ConquerPoints >= price)
            {
                client.Entity.TrySpendConquerPoints((uint)(price));
                if (talisman.SocketOne == Game.Enums.Gem.NoSocket)
                {
                    talisman.SocketOne = Game.Enums.Gem.EmptySocket;
                }
                else if (talisman.SocketTwo == Game.Enums.Gem.NoSocket)
                {
                    talisman.SocketTwo = Game.Enums.Gem.EmptySocket;
                }

                talisman.SocketProgress = 0;
                Database.ConquerItemTable.UpdateSockets(talisman);
                talisman.Mode = Game.Enums.ItemMode.Update;
                talisman.Send(client);
            }
            client.LoadItemStats();
        }
        public static void EnchantItem(ItemUsage itemUsage, GameState client)
        {
            Revora.Game.Features.ItemEnchantment.Enchant(client, itemUsage);
        }
        private static ushort GetSpellIdFromItem(uint ID)
        {
            switch (ID)
            {
                case 3003227: return 1001;
                case 3003228: return 11190;
                case 3003229: return 1046;
                case 3003230: return 10415;
                case 3003231: return 10381;
                case 3003232: return 1002;
                case 3003311: return 1115;
                case 3003312: return 10381;
            }
            return 0;
        }
        public static void UseItem(ConquerItem item, GameState client)
        {
            if (Revora.Game.Features.GuildSeasonWar.TryUseBomb(item, client)) return;
            if (Revora.Game.Features.FlameLitQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.EliminationQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.HouseFurniture.TryUse(item, client)) return;
            if (Revora.Game.Features.NinjaEpicQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.MonkEpicQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.TaoistEpicQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.PirateEpicWorld.TryUse(item, client)) return;
            if (Revora.Game.Features.WarriorEpicQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.TomboyQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DesertTreasureQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.AttackTalismanQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DefenseTalismanQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DesertExpeditionQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.FamilyFeudQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.HeroWingsQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DesertBloodQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.GhostCityQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.MadManQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.FranticMonkeysQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.MysticRingQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.SpunGoldQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.PowerBookQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.MonkSutta.TryUse(item, client)) return;
            if (Revora.Game.Features.SwordMasterEncounter.TryUse(item, client)) return;
            if (Revora.Game.Features.PurpleBansheeEncounter.TryUse(item, client)) return;
            if (Revora.Game.Features.TeratoEncounter.TryUse(item, client)) return;
            if (Revora.Game.Features.DragonSlayerHunt.TryUse(item, client)) return;
            if (Revora.Game.Features.RetiredHairDyes.TryUse(item, client)) return;
            if (Revora.Game.Features.RetiredBlackTulip.TryUse(item, client)) return;
            if (Revora.Game.Features.PrayingStones.TryUse(item, client)) return;
            if (Revora.Game.Features.WeaponSkillBooks.TryUse(item, client)) return;
            if (Revora.Game.Features.ExperienceConsumables.TryUse(item, client)) return;
            if (Revora.Game.Features.ExperiencePotions.TryUse(item, client)) return;
            if (Revora.Game.Features.TournamentExpPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.ExperienceProtection.TryUse(item, client)) return;
            if (Revora.Game.Features.ExpCareCombine.TryUse(item, client)) return;
            if (Revora.Game.Features.TimeDropHourglasses.TryUse(item, client)) return;
            if (Revora.Game.Features.WelcomePacks.TryUse(item, client)) return;
            if (Revora.Game.Features.StarterPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.RevoraStarterPack.TryUse(item, client)) return;
            if (Revora.Game.Features.P7SoulChoicePack.TryUse(item, client)) return;
            if (Revora.Game.Features.RebirthGarmentPack.TryUse(item, client)) return;
            if (Revora.Game.Features.ConquerPointBags.TryUse(item, client)) return;
            if (Revora.Game.Features.FixedOutputPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.DragonBallPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.PrizeCenterPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.EasterGoldPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.DailySignInPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.AncientStoneCombine.TryUse(item, client)) return;
            if (Revora.Game.Features.StarStoneCombine.TryUse(item, client)) return;
            if (Revora.Game.Features.AttackDefensePotions.TryUse(item, client)) return;
            if (Revora.Game.Features.ChiPointItems.TryUse(item, client)) return;
            if (Revora.Game.Features.SilverStudyBags.TryUse(item, client)) return;
            if (Revora.Game.Features.SilverPouches.TryUse(item, client)) return;
            if (Revora.Game.Features.StudyBooks.TryUse(item, client)) return;
            if (Revora.Game.Features.ChiTokenCash.TryUse(item, client)) return;
            if (Revora.Game.Features.QuestGiftPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.PenitenceAmulets.TryUse(item, client)) return;
            if (Revora.Game.Features.ElitePkPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.EliteNestedPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.P7SoulPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.ArenaPrizeConversions.TryUse(item, client)) return;
            if (Revora.Game.Features.MagnoliaQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DailyPriceQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.AnniversaryQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DailySoulBall.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityJades.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityTreasury.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityDarkHorn.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityRelics.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityQuest.TryUse(item, client)) return;
            if (Revora.Game.Features.DisCityPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.ExorcismRewards.TryUse(item, client)) return;
            if (Revora.Game.Features.PowerArenaPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.HealingMedicine.TryUse(item, client)) return;
            if (Revora.Game.Features.BloodshedSeaBoosts.TryUse(item, client)) return;
            if (Revora.Game.Features.HonorRefineryPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.GainRefineryPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.HossuHairPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.RedRunePacks.TryUse(item, client)) return;
            if (Revora.Game.Features.YellowBlueRunePacks.TryUse(item, client)) return;
            if (Revora.Game.Features.PlusOneStonePacks.TryUse(item, client)) return;
            if (Revora.Game.Features.HigherStonePacks.TryUse(item, client)) return;
            if (Revora.Game.Features.GemDragonSoulPacks.TryUse(item, client)) return;
            if (Revora.Game.Features.SummerCrafting.TryUse(item, client)) return;
            if (Revora.Game.Features.LabyrinthItems.TryUse(item, client)) return;
            Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.ID, 0);
            if (client.Booth != null)
            {
                return;
            }

            client.Entity.UseItem = true;
            client.SpiltStack = true;
            switch (item.ID)
            {
                #region Paquete de HP para Hossu
                case 3006319:
                    throw new InvalidOperationException("Hossu hair pack must use the owned service.");
                #endregion
                #region VIPLevelDays                
                case 3004121://2Dias 
                    {
                        if (client.Entity.VIPLevel == 5)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Entity.VIPLevel = 6;
                            client.Entity.VIPLevelDays = 6;
                            client.Entity.VIPDays = 2;
                            client.MessageBox("Congratulations! You have learned VIP 6. Enjoy your 2 days of paid VIP.");

                        }
                        else
                        {
                            client.MessageBox("Sorry, you cannot learn VIP 6 because you are not VIP 5. ");
                        }
                        break;
                    }
                case 3004122://5Dias
                    {
                        if (client.Entity.VIPLevel == 5)
                        {
                            if (client.Entity.VIPLevel <= 6 && client.Entity.VIPDays <= 2)
                            {
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                client.Entity.VIPLevel = 6;
                                client.Entity.VIPLevelDays = 6;
                                client.Entity.VIPDays = 5;
                                client.MessageBox("Congratulations! You have learned VIP 6. Enjoy your 5 days of paid VIP.");
                            }
                            if (client.Entity.VIPLevel <= 6 && client.Entity.VIPDays > 2)
                            {
                                client.MessageBox("You already have more than 3 days of VIP 6. This VIP 6 cannot be stacked.");
                            }

                        }
                        else
                        {
                            client.MessageBox("Sorry, you cannot learn VIP 6 because you are not VIP 5. ");
                        }
                        break;
                    }
                case 3004123://15Dias
                    {
                        if (client.Entity.VIPLevel == 5)
                        {
                            if (client.Entity.VIPLevel <= 6 && client.Entity.VIPDays <= 5)
                            {
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                client.Entity.VIPLevel = 6;
                                client.Entity.VIPLevelDays = 6;
                                client.Entity.VIPDays = 15;
                                client.MessageBox("Congratulations! You have learned VIP 6. Enjoy your 15 days of paid VIP.");
                            }
                            if (client.Entity.VIPLevel <= 6 && client.Entity.VIPDays > 5)
                            {
                                client.MessageBox("You already have more than 5 days of VIP 6. This VIP 6 cannot be stacked.");
                            }

                        }
                        else
                        {
                            client.MessageBox("Sorry, you cannot learn VIP 6 because you are not VIP 5. ");
                        }
                        break;
                    }
                case 3004124://30Dias
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.VIPLevel = 7;
                        client.Entity.VIPLevelDays = 7;
                        client.Entity.VIPDays += 30;
                        client.MessageBox("Congratulations! You have learned VIP 7. Enjoy your 30 days of paid VIP.");
                        break;
                    }
                case 3000794:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.VIPLevel = 7;
                        client.Entity.VIPLevelDays = 7;
                        client.Entity.VIPDays += 1;
                        client.MessageBox("Congratulations! You have claimed 1 day of VIP 7 from the Christmas gift.");
                        break;
                    }
                #endregion  

                #region item del server
                #region EXPCarePillScrap
                case 3002560:
                    throw new InvalidOperationException("EXPCarePill scrap combine must use the owned service.");
                #endregion
                #region LuxuryGoldEquipmentPack
                //paraAlgunPremioBueno jheisson
                case 3005368:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random Rand = new Random();
                        int dis = Rand.Next(1, 4);
                        if (dis == 1) { client.Inventory.Add(194300, 0, 1); }
                        if (dis == 2) { client.Entity.AddConquerPoints(5); }
                        if (dis == 3) { client.Entity.AddConquerPoints(6); }
                        if (dis == 4) { client.Entity.AddConquerPoints(7); }
                        break;
                    }
                #endregion
                #region JiangHu | Chi
                case 3001607://Chi
                    {
                        if (client.ChiPowers.Count > 0)
                        {
                            for (int i = 0; i < client.ChiPowers.Count; i++)
                            {
                                var Mode = i + 1;
                                for (int ii = 0; ii < 4; ii++)
                                {
                                    var pos = ii;
                                    var powers = client.ChiPowers[Mode - 1];
                                    var attributes = powers.Attributes;
                                    attributes[pos].Value = (ushort)Enums.ChiMaxValues1(attributes[pos].Type);
                                    powers.CalculatePoints();
                                    Database.ChiTable.Sort((Enums.ChiPowerType)Mode);
                                    powers.Power = (Enums.ChiPowerType)Mode;
                                    client.Send(new ChiPowers(true).Query(client));
                                    #region update ranking
                                    Database.ChiTable.ChiData[] array = null;
                                    switch ((Enums.ChiPowerType)Mode)
                                    {
                                        case Game.Enums.ChiPowerType.Dragon:
                                            array = Database.ChiTable.Dragon;
                                            break;

                                        case Game.Enums.ChiPowerType.Phoenix:
                                            array = Database.ChiTable.Phoenix;
                                            break;

                                        case Game.Enums.ChiPowerType.Tiger:
                                            array = Database.ChiTable.Tiger;
                                            break;

                                        case Game.Enums.ChiPowerType.Turtle:
                                            array = Database.ChiTable.Turtle;
                                            break;
                                    }
                                    foreach (var chiData in array)
                                    {
                                        if (Kernel.GamePool.ContainsKey(chiData.UID))
                                        {
                                            var pClient = Kernel.GamePool[chiData.UID];
                                            if (pClient == null)
                                            {
                                                continue;
                                            }

                                            if (pClient.ChiData == null)
                                            {
                                                continue;
                                            }

                                            GeneralData.SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)Mode, pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode), pClient.ChiData.SelectPoints((Enums.ChiPowerType)Mode));
                                            if (pClient.Entity.UID == client.Entity.UID || pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode) < 50)
                                            {
                                                pClient.LoadItemStats();
                                            }
                                        }
                                    }
                                    #endregion
                                    Database.ChiTable.Save(client);
                                }
                            }
                        }
                        else
                        {
                            client.MessageBox("You have not opened Chi yet.");
                            return;
                        }
                        client.Inventory.Remove(item, Enums.ItemUse.Remove);
                        break;
                    }
                case 3003650://JiangHu
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            for (int i = 0; i < client.Entity.MyJiang.Stagers.Length; i++)
                            {
                                var stage = client.Entity.MyJiang.Stagers[i];
                                if (!stage.Activate)
                                {
                                    continue;
                                }

                                for (int ii = 0; ii < stage.Stars.Length; ii++)
                                {
                                    var star = stage.Stars[ii];
                                    if (!star.Activate)
                                    {
                                        continue;
                                    }

                                    star.Level = 6;
                                    star.UID = client.Entity.MyJiang.ValueToRoll(star.Typ, star.Level);
                                    JiangHuUpdate upd = new JiangHuUpdate();
                                    upd.Atribute = star.UID;
                                    upd.FreeCourse = client.Entity.MyJiang.FreeCourse;
                                    upd.Stage = (byte)i;
                                    upd.Star = (byte)ii;
                                    upd.FreeTimeTodeyUsed = (byte)client.Entity.MyJiang.FreeTimeTodeyUsed;
                                    upd.RoundBuyPoints = client.Entity.MyJiang.RoundBuyPoints;
                                    client.Send(upd.ToArray());

                                }
                            }
                            client.LoadItemStats();
                        }
                        else
                        {
                            client.MessageBox("You have not opened Jiang Hu yet.");
                            return;
                        }
                        client.Inventory.Remove(item, Enums.ItemUse.Remove);
                        break;
                    }
                #endregion
                #region WarOfHeroes item's
                case 3005117:
                case 3005118:
                case 3005119:
                case 3005120:
                    throw new InvalidOperationException("Silver/study bag must use the owned service.");
                case 3001264:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        var array = Database.ConquerItemInformation.BaseInformations.Values
                             .Where(p => (p.ID >= 181305 && p.ID <= 194300)).ToArray();
                        client.Inventory.AddBoundItem(array[Kernel.Random.Next(array.Length)].ID, 0, 1);
                        break;
                    }
                case 3005131:
                    {
                        break;
                    }
                case 3005126:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                case 3005125:
                case 3005124:
                case 3005123:
                case 3005121:
                case 3005122:
                    throw new InvalidOperationException("Silver/study bag must use the owned service.");
                case 3005130:
                case 3005132:
                case 3005133:
                case 3005135:
                    throw new InvalidOperationException("Gem or Dragon Soul pack must use the owned service.");
                case 3005129:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                case 3005127:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                case 3005128:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                #endregion
                #region TinyHorseRacingPointsBag
                case 728899:
                    throw new InvalidOperationException("Prize Center pack must use the owned service.");
                #endregion
                #region TinyClass2MoneyPouch
                case 728898:
                    throw new InvalidOperationException("Prize Center pack must use the owned service.");
                #endregion
                #region TinyClass2MoneyPouch
                case 728900:
                    throw new InvalidOperationException("Prize Center pack must use the owned service.");
                #endregion
                #region BloodshedScroll
                case 3004296:
                    {
                        client.MessageBox("NemesisTyrant~hasn`t~shown~up~or~has~been~defeated.~You~still~want~to~use~the~scroll?",
                        (p) =>
                        {
                            p.Entity.Teleport(3846, 301, 288);
                            p.Send("NemesisTyrant~hasn`t~shown~up~or~has~been~defeated.");
                            client.Inventory.Remove(item, Enums.ItemUse.Remove);
                        });
                        break;
                    }
                #endregion
                #region ShadowJade
                case 720599:
                    {
                        if (client.Entity.MapID == 2055)
                        {
                            client.Entity.Teleport(2055, 537, 568);
                        }
                        else
                        {
                            client.MessageBox("This can only be used on the 5th floor of the Frozen Grotto.", null, null, 0);
                        }

                        break;
                    }
                #endregion
                #region Bags Cps
                case 729911:
                case 3001133:
                case 720657:
                case 729912:
                case 720656:
                case 3001134:
                case 3001135:
                case 720655:
                case 720658:
                case 720663:
                case 720659:
                case 720660:
                case 720664:
                case 720665:
                case 720675:
                case 720653:
                case 720681:
                case 720676:
                case 3001136:
                case 720677:
                case 720682:
                case 720687:
                case 720661:
                case 720683:
                case 720688:
                case 720693:
                case 720666:
                case 720689:
                case 720694:
                case 720662:
                case 720695:
                case 720678:
                case 720667:
                case 720684:
                case 720685:
                    throw new InvalidOperationException("CP bag must use the owned service.");
                #endregion
                #region ChiPoints Items
                #region SmallChiPill and catalog Chi pills
                case 3000051:
                case 729476:
                case 729477:
                case 729478:
                case 729479:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                #endregion
                #region VitalPill
                case 729481:
                case 729242:
                    throw new InvalidOperationException("Chi item must use the owned service.");
                #endregion
                #endregion
                #region Bloodshed~Sea item's
                case 3004230:
                case 3004182:
                case 3004231:
                case 3004232:
                case 3004233:
                case 3004234:
                case 3004235:
                case 3004236:
                case 3004237:
                case 3004238:
                    throw new InvalidOperationException("Bloodshed Sea boost must use the owned service.");
                #endregion
                #region EpicaDeMonje
                case 3006700:
                    {
                        client.Inventory.Add(622439, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Kernel.SendWorldMessage(new Message(" " + client.Entity.Name + " has obtained a Monk epic [SuperiorPilarTodoPoderoso]", System.Drawing.Color.Cyan, Message.Center), Server.GamePool);
                        break;
                    }
                #endregion
                #region EpicaDepirata
                case 3307451:
                    {
                        client.Inventory.Add(670439, 0, 1);
                        client.Inventory.Add(671439, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Kernel.SendWorldMessage(new Message(" " + client.Entity.Name + " has obtained Pirate epics [MightyRapierofViolence + OceanDominator]", System.Drawing.Color.Cyan, Message.Center), Server.GamePool);
                        break;
                    }
                #endregion
                #region DivinePanacea
                case 3006016://3006226
                    {
                        client.Inventory.Add(620439, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Kernel.SendWorldMessage(new Message(" " + client.Entity.Name + " has obtained a Taoist epic (TizonaImperial)", System.Drawing.Color.Cyan, Message.Center), Server.GamePool);
                        break;
                    }
                #endregion
                #region SuperHossu
                case 3006226:
                    {
                        client.Inventory.Add(619439, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Kernel.SendWorldMessage(new Message(" " + client.Entity.Name + " has obtained a Taoist epic (HossuDeUniverso)", System.Drawing.Color.Cyan, Message.Center), Server.GamePool);
                        break;
                    }
                #endregion
                #region EpicadeTroy
                case 3003340:
                    {
                        client.Inventory.Add(614439, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Kernel.SendWorldMessage(new Message(" " + client.Entity.Name + " has obtained a Trojan epic (ColmilloCruzable)", System.Drawing.Color.Cyan, Message.Center), Server.GamePool);
                        break;
                    }
                #endregion
                #region Pandora invitation
                case 3006347:
                    {
                        // Both installed catalogs identify Pandora's Call. Preserve
                        // the invitation until its own destination is implemented.
                        client.Send("The invitation's destination is not yet open. Keep it for later.");
                        break;
                    }
                #endregion
                #region booth item
                case 3001523:
                    {
                        #region booth
                        {
                            if (client.Booth == null)
                            {
                                client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[1036].Status });
                                client.Booth = new Game.ConquerStructures.Booth(client, new Data(true) { UID = client.Entity.UID });
                                client.Send(new Data(true) { ID = GamePackets.Data.ChangeAction, UID = client.Entity.UID, dwParam = 0 });
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            break;
                        }
                        #endregion booth
                    }
                #endregion booth

                #region Tarea de Perfeccion
                #region PiedraEstrellaBrillante
                case 3009001:
                    throw new InvalidOperationException("Star stone combine must use the owned service.");
                #endregion
                #region PiedraEstrellaRadiante
                case 3009002:
                    Revora.Game.Features.ArenaPrizeConversions.TryUse(item, client);
                    break;
                #endregion
                #endregion

                #region Tarea de runas
                #region fracmentos Roja
                case 3306369:
                    throw new InvalidOperationException("Red rune item must use the owned service.");
                #endregion
                #region fracmentos amarilla
                case 3306370:
                    throw new InvalidOperationException("Yellow/blue rune item must use the owned service.");
                #endregion
                #region fracmentos azul
                case 3306371:
                    throw new InvalidOperationException("Yellow/blue rune item must use the owned service.");
                #endregion
                #region PaqRunaRoja
                case 3306509:
                    throw new InvalidOperationException("Red rune item must use the owned service.");
                #endregion
                #region PaqRunaAmarilla
                case 3306510:
                    throw new InvalidOperationException("Yellow/blue rune item must use the owned service.");
                #endregion
                #region PaqRunaAzul
                case 3306511:
                    throw new InvalidOperationException("Yellow/blue rune item must use the owned service.");
                #endregion
                #endregion


                #region Tarea de Equipo P7
                #region PiezaAlmaSantaE7
                case 3004243:
                    Revora.Game.Features.ArenaPrizeConversions.TryUse(item, client);
                    break;
                #endregion
                #region PiezaAlmaEquipamientoE7
                case 3004244:
                    Revora.Game.Features.ArenaPrizeConversions.TryUse(item, client);
                    break;
                #endregion
                #region PiezaSagradaRefinada
                case 3004245:
                    Revora.Game.Features.ArenaPrizeConversions.TryUse(item, client);
                    break;
                #endregion
                #region PiedraAntigua
                case 3004246:
                    throw new InvalidOperationException("Ancient Stone combine must use the owned service.");
                #endregion
                #region PaqAlmaSantaE4
                case 3005891:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 16);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(800014, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(800015, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(800016, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(800214, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(800253, 0, 1);

                        }
                        if (Nr == 6)
                        {
                            client.Inventory.Add(800512, 0, 1);

                        }
                        if (Nr == 7)
                        {
                            client.Inventory.Add(800613, 0, 1);

                        }
                        if (Nr == 8)
                        {
                            client.Inventory.Add(800720, 0, 1);

                        }
                        if (Nr == 9)
                        {
                            client.Inventory.Add(800803, 0, 1);

                        }
                        if (Nr == 10)
                        {
                            client.Inventory.Add(800808, 0, 1);
                        }
                        if (Nr == 11)
                        {
                            client.Inventory.Add(800913, 0, 1);
                        }
                        if (Nr == 12)
                        {
                            client.Inventory.Add(823052, 0, 1);

                        }
                        if (Nr == 13)
                        {
                            client.Inventory.Add(823053, 0, 1);

                        }
                        if (Nr == 14)
                        {
                            client.Inventory.Add(823054, 0, 1);

                        }
                        if (Nr == 15)
                        {
                            client.Inventory.Add(801002, 0, 1);

                        }
                        if (Nr == 16)
                        {
                            client.Inventory.Add(801102, 0, 1);

                        }
                        break;
                    }
                #endregion
                #region PaqAlmaSantaE5// reparar
                case 3005892:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 5);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(800415, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(820056, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(820057, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(821028, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(821030, 0, 1);

                        }
                        break;
                    }
                #endregion
                #region PaqAlmaEquipamientoE7
                case 3004248:
                    Revora.Game.Features.P7SoulPacks.TryUse(item, client);
                    break;
                #endregion
                #region L5PaqEncantamiento
                case 3005894:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 43);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(724439, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(721352, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(721362, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(721377, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(721384, 0, 1);

                        }
                        if (Nr == 6)
                        {
                            client.Inventory.Add(721394, 0, 1);

                        }
                        if (Nr == 7)
                        {
                            client.Inventory.Add(721404, 0, 1);

                        }
                        if (Nr == 8)
                        {
                            client.Inventory.Add(721409, 0, 1);

                        }
                        if (Nr == 9)
                        {
                            client.Inventory.Add(721424, 0, 1);

                        }
                        if (Nr == 10)
                        {
                            client.Inventory.Add(721453, 0, 1);
                        }
                        if (Nr == 11)
                        {
                            client.Inventory.Add(721463, 0, 1);
                        }
                        if (Nr == 12)
                        {
                            client.Inventory.Add(721487, 0, 1);

                        }
                        if (Nr == 13)
                        {
                            client.Inventory.Add(721492, 0, 1);

                        }
                        if (Nr == 14)
                        {
                            client.Inventory.Add(721497, 0, 1);

                        }
                        if (Nr == 15)
                        {
                            client.Inventory.Add(724357, 0, 1);

                        }
                        if (Nr == 16)
                        {
                            client.Inventory.Add(724377, 0, 1);

                        }
                        if (Nr == 17)
                        {
                            client.Inventory.Add(724384, 0, 1);

                        }
                        if (Nr == 18)
                        {
                            client.Inventory.Add(724389, 0, 1);

                        }
                        if (Nr == 19)
                        {
                            client.Inventory.Add(724394, 0, 1);

                        }
                        if (Nr == 20)
                        {
                            client.Inventory.Add(724404, 0, 1);

                        }
                        if (Nr == 21)
                        {
                            client.Inventory.Add(724409, 0, 1);

                        }
                        if (Nr == 22)
                        {
                            client.Inventory.Add(724419, 0, 1);

                        }
                        if (Nr == 23)
                        {
                            client.Inventory.Add(724424, 0, 1);

                        }
                        if (Nr == 24)
                        {
                            client.Inventory.Add(724429, 0, 1);

                        }
                        if (Nr == 25)
                        {
                            client.Inventory.Add(724434, 0, 1);

                        }
                        if (Nr == 26)
                        {
                            client.Inventory.Add(724439, 0, 1);

                        }
                        if (Nr == 27)
                        {
                            client.Inventory.Add(724444, 0, 1);

                        }
                        if (Nr == 28)
                        {
                            client.Inventory.Add(724453, 0, 1);

                        }
                        if (Nr == 29)
                        {
                            client.Inventory.Add(724463, 0, 1);

                        }
                        if (Nr == 30)
                        {
                            client.Inventory.Add(724472, 0, 1);

                        }
                        if (Nr == 31)
                        {
                            client.Inventory.Add(724477, 0, 1);

                        }
                        if (Nr == 32)
                        {
                            client.Inventory.Add(724482, 0, 1);

                        }
                        if (Nr == 33)
                        {
                            client.Inventory.Add(724487, 0, 1);

                        }
                        if (Nr == 34)
                        {
                            client.Inventory.Add(724492, 0, 1);

                        }
                        if (Nr == 35)
                        {
                            client.Inventory.Add(724497, 0, 1);

                        }
                        if (Nr == 36)
                        {
                            client.Inventory.Add(725176, 0, 1);

                        }
                        if (Nr == 37)
                        {
                            client.Inventory.Add(725181, 0, 1);

                        }
                        if (Nr == 38)
                        {
                            client.Inventory.Add(725186, 0, 1);

                        }
                        if (Nr == 39)
                        {
                            client.Inventory.Add(725191, 0, 1);

                        }
                        if (Nr == 40)
                        {
                            client.Inventory.Add(725196, 0, 1);

                        }
                        if (Nr == 41)
                        {
                            client.Inventory.Add(725201, 0, 1);

                        }
                        if (Nr == 42)
                        {
                            client.Inventory.Add(725206, 0, 1);

                        }
                        if (Nr == 43)
                        {
                            client.Inventory.Add(725211, 0, 1);

                        }
                        break;
                    }
                #endregion
                #region PaqSagradaRefinadaE7
                case 3004249:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 31);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(3004136, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(3004146, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(3004155, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(3004157, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(3004137, 0, 1);

                        }
                        if (Nr == 6)
                        {
                            client.Inventory.Add(3004156, 0, 1);

                        }
                        if (Nr == 7)
                        {
                            client.Inventory.Add(3004147, 0, 1);

                        }
                        if (Nr == 8)
                        {
                            client.Inventory.Add(3004158, 0, 1);

                        }
                        if (Nr == 9)
                        {
                            client.Inventory.Add(3004138, 0, 1);

                        }
                        if (Nr == 10)
                        {
                            client.Inventory.Add(3004148, 0, 1);
                        }
                        if (Nr == 11)
                        {
                            client.Inventory.Add(3004139, 0, 1);
                        }
                        if (Nr == 12)
                        {
                            client.Inventory.Add(3004149, 0, 1);

                        }
                        if (Nr == 13)
                        {
                            client.Inventory.Add(3004140, 0, 1);

                        }
                        if (Nr == 14)
                        {
                            client.Inventory.Add(3004141, 0, 1);

                        }
                        if (Nr == 15)
                        {
                            client.Inventory.Add(3004150, 0, 1);

                        }
                        if (Nr == 16)
                        {
                            client.Inventory.Add(3004159, 0, 1);

                        }
                        if (Nr == 17)
                        {
                            client.Inventory.Add(3004163, 0, 1);

                        }
                        if (Nr == 18)
                        {
                            client.Inventory.Add(3004164, 0, 1);

                        }
                        if (Nr == 19)
                        {
                            client.Inventory.Add(3004142, 0, 1);

                        }
                        if (Nr == 20)
                        {
                            client.Inventory.Add(3004151, 0, 1);

                        }
                        if (Nr == 21)
                        {
                            client.Inventory.Add(3004160, 0, 1);

                        }
                        if (Nr == 22)
                        {
                            client.Inventory.Add(3004143, 0, 1);

                        }
                        if (Nr == 23)
                        {
                            client.Inventory.Add(3004152, 0, 1);

                        }
                        if (Nr == 24)
                        {
                            client.Inventory.Add(3004161, 0, 1);

                        }
                        if (Nr == 25)
                        {
                            client.Inventory.Add(3004165, 0, 1);

                        }
                        if (Nr == 26)
                        {
                            client.Inventory.Add(3004166, 0, 1);

                        }
                        if (Nr == 27)
                        {
                            client.Inventory.Add(3004144, 0, 1);

                        }
                        if (Nr == 28)
                        {
                            client.Inventory.Add(3004153, 0, 1);

                        }
                        if (Nr == 29)
                        {
                            client.Inventory.Add(3004145, 0, 1);

                        }
                        if (Nr == 30)
                        {
                            client.Inventory.Add(3004154, 0, 1);

                        }
                        if (Nr == 31)
                        {
                            client.Inventory.Add(3004162, 0, 1);

                        }
                        break;
                    }
                #endregion
                #region PaqAlmaSantaE7
                case 3004247:
                    Revora.Game.Features.P7SoulPacks.TryUse(item, client);
                    break;
                #endregion
                #region E6PaqueteArmaSanta
                case 729122:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 18);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(800000, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(800017, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(800110, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(800254, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(800320, 0, 1);

                        }
                        if (Nr == 6)
                        {
                            client.Inventory.Add(800421, 0, 1);

                        }
                        if (Nr == 7)
                        {
                            client.Inventory.Add(800513, 0, 1);

                        }
                        if (Nr == 8)
                        {
                            client.Inventory.Add(801306, 0, 1);

                        }
                        if (Nr == 9)
                        {
                            client.Inventory.Add(800722, 0, 1);

                        }
                        if (Nr == 10)
                        {
                            client.Inventory.Add(800804, 0, 1);
                        }
                        if (Nr == 11)
                        {
                            client.Inventory.Add(800809, 0, 1);
                        }
                        if (Nr == 12)
                        {
                            client.Inventory.Add(800916, 0, 1);

                        }
                        if (Nr == 13)
                        {
                            client.Inventory.Add(801003, 0, 1);

                        }
                        if (Nr == 14)
                        {
                            client.Inventory.Add(801103, 0, 1);

                        }

                        if (Nr == 15)
                        {
                            client.Inventory.Add(801103, 0, 1);
                        }
                        if (Nr == 16)
                        {
                            client.Inventory.Add(801214, 0, 1);

                        }

                        if (Nr == 17)
                        {
                            client.Inventory.Add(801212, 0, 1);
                        }
                        if (Nr == 18)
                        {
                            client.Inventory.Add(800616, 0, 1);
                        }
                        if (Nr == 19)
                        {
                            client.Inventory.Add(801304, 0, 1);
                        }
                        break;
                    }
                #endregion
                #region E6PaqEquipoSanta
                case 3002131:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random R = new Random();
                        int Nr = R.Next(1, 10);
                        if (Nr == 1)
                        {
                            client.Inventory.Add(822053, 0, 1);
                        }
                        if (Nr == 2)
                        {
                            client.Inventory.Add(822055, 0, 1);

                        }
                        if (Nr == 3)
                        {
                            client.Inventory.Add(823055, 0, 1);

                        }
                        if (Nr == 4)
                        {
                            client.Inventory.Add(823056, 0, 1);

                        }
                        if (Nr == 5)
                        {
                            client.Inventory.Add(823057, 0, 1);

                        }
                        if (Nr == 6)
                        {
                            client.Inventory.Add(824017, 0, 1);

                        }
                        if (Nr == 7)
                        {
                            client.Inventory.Add(821031, 0, 1);

                        }
                        if (Nr == 8)
                        {
                            client.Inventory.Add(821032, 0, 1);

                        }
                        if (Nr == 9)
                        {
                            client.Inventory.Add(820071, 0, 1);

                        }
                        if (Nr == 10)
                        {
                            client.Inventory.Add(820072, 0, 1);
                        }
                        break;
                    }
                #endregion
                #region PaqueteSanto [nuevo]
                case 720121:
                    throw new InvalidOperationException("Easter gold pack must use the owned service.");
                #endregion
                #endregion
                #region Sistema de #0 dias en Slayer2.0
                case 3100011:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                case 3100013:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                case 3100014:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                case 3100015:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                case 3100016:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                case 3100017:
                    throw new InvalidOperationException("Daily sign-in pack must use the owned service.");
                //case 2100085:
                //{
                //    if (client.Inventory.Contains(2100085, 12) && client.Inventory.Count <= 31)
                //    {
                //        client.Inventory.Add(2100075, 0, 1);
                //        client.Inventory.Remove(2100085, 12);
                //        client.Send("Gracias por tu esfuerzo Slayer te da de Obsequio un SuperCopaSlayer");
                //    }
                //    else
                //    {
                //        client.Send("Usted no contiene las 12 Copas Para Tenr la Copa de Slayer 2.0");
                //    }
                //    break;
                //}
                #endregion
                #region Paquetito de RUEDA ruleta
                case 3600023:// Lo Podes Cambiar a Gusto y crear uno Vos!
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Send(new Network.GamePackets.RuedaDeSuerte()
                        {
                            Tipos = Network.GamePackets.RuedaDeSuerte.Tipo.Abrir,
                            ID = 3652
                        });
                        client.LastTaskRewardID = 0;
                        break;
                    }
                #endregion
                #region inbocarBoss[VIP5]
                case 3000787:
                    {
                        if (client.Entity.VIPLevel <= 4)
                        {

                            Database.MonsterInformation mob;
                            Database.MonsterInformation.MonsterInformations.TryGetValue(7165, out mob);
                            client.Map.Spawnthis(mob, client, client.Entity.MapID, (ushort)(client.Entity.X - 2), (ushort)(client.Entity.Y - 2));
                            client.SendScreenSpawn(client.Entity, true);
                            client.Screen.FullWipe();
                            client.Screen.Reload(null);
                            client.Inventory.Remove(item, Enums.ItemUse.Remove);
                            client.Send(new Message("DrunkWatchman has appeared. Hunt it to obtain VIP 5.", Color.White, Message.System));
                        }
                        else
                        {
                            client.Send(new Message("You already have VIP 5 or higher and cannot summon DrunkWatchman.", Color.White, Message.System));
                        }

                        break;
                    }
                #endregion
                #region inbocarBoss[GM]
                case 3000788:
                    {

                        if (client.Entity.VIPLevel >= 8)
                        {

                            Database.MonsterInformation mob;
                            Database.MonsterInformation.MonsterInformations.TryGetValue(2617, out mob);
                            client.Map.Spawnthis(mob, client, client.Entity.MapID, (ushort)(client.Entity.X - 2), (ushort)(client.Entity.Y - 2));
                            client.SendScreenSpawn(client.Entity, true);
                            client.Screen.FullWipe();
                            client.Screen.Reload(null);
                            //client.Inventory.Remove(item, Enums.ItemUse.Remove);
                            client.Send(new Message("You have summoned WhiteDragon.", Color.White, Message.System));
                        }
                        else
                        {
                            client.Send(new Message("You are not an administrator and cannot summon this monster.", Color.White, Message.System));
                        }

                        break;
                    }
                #endregion
                #region EspatoMagico(200Mil)
                case 3006594:
                    throw new InvalidOperationException("Silver pouch must use the owned service.");
                #endregion
                #region BILLETERAS DE SLAYER
                #region Class9MoneyBag
                case 723721:
                    throw new InvalidOperationException("Silver pouch must use the owned service.");
                #endregion
                #region Class10MoneyBag
                case 723722:
                    {
                        //client.Inventory.Remove(item, Game.Enums.ItemUse.RemoveFromStack);
                        client.Entity.AdjustMoney((decimal)(300000000));
                        client.Inventory.Remove(723722, 1);
                        break;
                    }
                #endregion
                #region TopMoneyBag
                case 723723:
                    throw new InvalidOperationException("Silver pouch must use the owned service.");
                #endregion
                #region LuxuriMoneyBag
                case 3007368:
                    {
                        client.Inventory.Remove(3007368, 1);
                        client.Entity.AdjustMoney((decimal)(500000000000));
                        break;
                    }
                #endregion
                #region KingdomMoneyBag
                case 3007365:
                    {
                        client.Inventory.Remove(3007365, 1);
                        client.Entity.AdjustMoney((decimal)(250000000000));
                        break;
                    }
                #endregion
                #region HarvestMoneyBag
                case 3007367:
                    {
                        client.Inventory.Remove(3007367, 1);
                        client.Entity.AdjustMoney((decimal)(50000000000));
                        break;
                    }
                #endregion
                #region AbundantMoneyBag
                case 3007366:
                    {
                        client.Inventory.Remove(3007366, 1);
                        client.Entity.AdjustMoney((decimal)(2000000000));
                        break;
                    }
                #endregion
                #endregion
                #region equipo P7
                //#region P7WeaponSoulPack
                //case 3004247:
                //    {
                //        Npcs dialog = new Npcs(client);
                //        dialog.Text("Hola. Usted puede seleccionar una Alma Santa E7.");
                //        dialog.Option("MonsterSaber", 1);
                //        dialog.Option("SkyHammer", 2);
                //        dialog.Option("SkyHalberd", 3);
                //        dialog.Option("SpiritShield", 4);
                //        dialog.Option("GhostKnife", 5);
                //        dialog.Option("SunBow", 6);
                //        dialog.Option("ShadowKatana", 7);
                //        dialog.Option("DemonScythe", 8);
                //        dialog.Option("BuddaBeads", 9);
                //        dialog.Option("DeathPistol", 10);
                //        dialog.Option("RepentRapier", 11);
                //        dialog.Option("TimeBacksword", 12);
                //        dialog.Option("Nunchaku", 13);
                //        dialog.Avatar(0);
                //        dialog.Send();
                //        client.ActiveNpc = item.ID;
                //        break;
                //    }
                //#endregion
                //#region P7EquipmentSoulPack
                //case 3004248:
                //    {
                //        Npcs dialog = new Npcs(client);
                //        dialog.Text("Hola. Usted puede seleccionar un Alma Santa E7");
                //        dialog.Option("CraneRing", 1);
                //        dialog.Option("FoxBoots", 2);
                //        dialog.Option("NetherArmor", 3);
                //        dialog.Option("HeavenNecklace", 4);
                //        dialog.Option("MoonHeadgear", 5);
                //        dialog.Option("FervorBag", 6);
                //        dialog.Option("RainbowBracelet", 7);
                //        dialog.Option("EclipseArmor", 8);
                //        dialog.Option("CraneBoots", 9);
                //        dialog.Option("IceHeadgear", 10);
                //        dialog.Option("SunHeadgear", 11);
                //        dialog.Option("StarHeadgear", 12);
                //        dialog.Option("DragonBoots", 13);
                //        dialog.Option("DragonRing", 14);
                //        dialog.Option("LionHeavyRing", 15);
                //        dialog.Option("TigerHeavyRing", 16);
                //        dialog.Avatar(0);
                //        dialog.Send();
                //        client.ActiveNpc = item.ID;
                //        break;
                //    }
                //#endregion
                //#region SacredRefineryPack
                //case 3004249:
                //    {
                //        Npcs dialog = new Npcs(client);
                //        dialog.Text("Hello. You can chose a Lve.6 Refinery.");
                //        dialog.Option("CriticalStrike [1-handed]", 15);
                //        dialog.Option("CriticalStrike [2-handed]", 16);
                //        dialog.Option("CriticalStrike [Ring]", 10);
                //        dialog.Option("CriticalStrike [Bow]", 14);
                //        dialog.Option("SkillC.Strike [Backsword]", 28);
                //        dialog.Option("Counteraction [Armor]", 3);
                //        dialog.Option("Counteraction [Necklace]", 11);
                //        dialog.Option("Breakthrough [Ring]", 12);
                //        dialog.Option("Breakthrough [Bracelet]", 13);
                //        dialog.Option("Breakthrough [1-handed]", 17);
                //        dialog.Option("Breakthrough [2-handed]", 18);
                //        dialog.Option("Breakthrough [Bow])", 19);
                //        dialog.Option("Detoxication [Headgear]", 21);
                //        dialog.Option("Detoxication [Armor]", 22);
                //        dialog.Option("Detoxication [Boots]", 23);
                //        dialog.Option("Detoxication [Necklace]", 24);
                //        dialog.Option("Detoxication [Bag]", 26);
                //        dialog.Option("Penetration [Headgear]", 25);
                //        dialog.Option("Penetration [Bag]", 7);
                //        dialog.Option("Penetration [Bracelet]", 27);
                //        dialog.Option("M-Defense [Necklace]", 1);
                //        dialog.Option("M-Defense [Bag]", 5);
                //        dialog.Option("M-Defense [Ring]", 8);
                //        dialog.Option("M-Defense [Bracelet]", 9);
                //        dialog.Option("Block [Headgear]", 4);
                //        dialog.Option("Block [Shield]", 20);
                //        dialog.Option("Immunity [Armor]", 2);
                //        dialog.Option("Immunity [Boots]", 6);
                //        dialog.Avatar(0);
                //        dialog.Send();
                //        client.ActiveNpc = item.ID;
                //        break;
                //    }
                //#endregion
                #endregion
                #region AccessoryScrapPack
                case 3004257:
                    throw new InvalidOperationException("Prize Center pack must use the owned service.");
                #endregion
                #region FirstCredit
                case 3001266://FortunePack
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.AdjustMoney((decimal)(200000000));
                        client.Send(new Message("You claimed 200,000,000 gold.", System.Drawing.Color.Red, Message.System));
                        break;
                    }
                case 3004085://CajaDeCPs
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.AddConquerPoints(100000000);
                        client.Send(new Message("You claimed 100,000,000 CPs!", System.Drawing.Color.Red, Message.System));
                        break;
                    }
                case 720654: // PaqueteSlayerPuntos
                    {
                        client.Entity.AddRevoraPoints(25000);
                        client.Inventory.Remove(item, Enums.ItemUse.Remove);
                        client.Send(new Message("You claimed 25,000 Revora Points!", System.Drawing.Color.Red, Message.System));
                        break;
                    }
                case 3300809://PiedraElite*10
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Inventory.AddBoundItem(3009002, 10, 1);
                        break;
                    }
                /*case 3001275://SuperWeapon
                    {
                        #region Trojan
                        if (client.Player.Class >= 10 && client.Player.Class <= 15)
                        {
                            client.Inventory.AddBoundItem(410009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Ninja
                        if (client.Player.Class >= 50 && client.Player.Class <= 55)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(601009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Archer
                        if (client.Player.Class >= 40 && client.Player.Class <= 45)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(500019, 0, 1);
                            break;
                        }
                        #endregion
                        #region Monk
                        if (client.Player.Class >= 60 && client.Player.Class <= 65)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(610009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Warrior
                        if (client.Player.Class >= 20 && client.Player.Class <= 25)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(561009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Fire|Water
                        if (client.Player.Class >= 100 && client.Player.Class <= 145)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(421009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Pirate
                        if (client.Player.Class >= 70 && client.Player.Class <= 75)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(611009, 0, 1);
                            break;
                        }
                        #endregion
                        #region LeeLong
                        if (client.Player.Class >= 80 && client.Player.Class <= 85)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(138000, 0, 1);
                            break;
                        }
                        #endregion
                        break;
                    }
                case 3001280://SuperArmor
                    {
                        #region Trojan
                        if (client.Player.Class >= 10 && client.Player.Class <= 15)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(130009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Ninja
                        if (client.Player.Class >= 50 && client.Player.Class <= 55)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(135009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Warrior
                        if (client.Player.Class >= 20 && client.Player.Class <= 25)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(133009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Monk
                        if (client.Player.Class >= 60 && client.Player.Class <= 65)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(136009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Archer
                        if (client.Player.Class >= 40 && client.Player.Class <= 45)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(131009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Fire|Water
                        if (client.Player.Class >= 100 && client.Player.Class <= 145)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(134009, 0, 1);
                            break;
                        }
                        #endregion
                        #region Pirate
                        if (client.Player.Class >= 70 && client.Player.Class <= 75)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(139009, 0, 1);
                            break;
                        }
                        #endregion
                        #region LeeLong
                        if (client.Player.Class >= 80 && client.Player.Class <= 85)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.AddBoundItem(617000, 0, 1);
                            break;
                        }
                        #endregion
                        break;
                    }*/
                case 3001267://+12WhiteSteed
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        _item.Bound = item.Bound;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 12;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 | 255 << 8;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                //case 3001282://PrayingStone(L)
                //    //{
                //    //    client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                //    //    client.Inventory.AddBoundItem(1200002, 0, 1);
                //    //    break;
                //    //}
                //case 3001289://PromotionGift
                //    //{
                //    client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                //    client.Inventory.AddBoundItem(721080, 0, 1);
                //    client.Inventory.AddBoundItem(721259, 0, 1);
                //    client.Inventory.AddBoundItem(1080001, 0, 1);
                //    break;
                //}
                #endregion
                #region Flag Pack's
                case 729793://ExemptionToken
                    {
                        client.Inventory.Add(723701, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 3002857://Erik`sFlag
                    {
                        uint itemid = 360049;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002856://CaptainMorgan`sFlag
                    {
                        uint itemid = 360048;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002855://PirateKing`sFlag
                    {
                        uint itemid = 360047;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002854://Blackbeard`sFlag
                    {
                        uint itemid = 360046;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002853://CaribbeanFlag
                    {
                        uint itemid = 360045;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002852://Drake`sFlag
                    {
                        uint itemid = 350045;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002851://CaptainKidd`sFlag
                    {
                        uint itemid = 350044;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002850://CrazyJack`sFlag
                    {
                        uint itemid = 350043;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002849://BlackPearlFlag
                    {
                        uint itemid = 350042;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002848://Viking`sFlag
                    {
                        uint itemid = 350041;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                #endregion
                #region MountArmorPack's
                case 3004351://LoveHorse
                    {
                        uint itemid = 200495;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3004350://MoneyHorse
                    {
                        uint itemid = 200494;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3004349://LionKing
                    {
                        uint itemid = 200433;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002749://ScaleDragon
                    {
                        uint itemid = 200488;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;

                    }
                case 3002748://DrummerPanda
                    {
                        uint itemid = 200476;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3002747://HeavenRhino
                    {
                        uint itemid = 200487;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3001119://FlameFox
                    {
                        uint itemid = 200474;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3000678://DancingSnake
                    {
                        uint itemid = 200459;
                        ConquerItem items = new ConquerItem(true);
                        items.ID = itemid;
                        items.Bless = 1;
                        items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                        if (client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd))
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }

                        break;
                    }
                case 3000271:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000270:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000269:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000268:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000267:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000266:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000265:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000264:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000263:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000262:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000261:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000260:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000259:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000258:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000257:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000256:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                case 3000254:
                    throw new InvalidOperationException("Mount pack must use the owned fixed-output service.");
                #endregion
                #region Skills Soul
                case 3003228:
                case 3003229:
                case 3003230:
                case 3003231:
                case 3003232:
                case 3003311:
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            ushort spellIdFromItem = ItemHandler.GetSpellIdFromItem(item.ID);
                            if (!client.Spells.ContainsKey(spellIdFromItem))
                            {
                                client.Send(new Message("Sorry you don't have this spell", Color.Red, Message.System));
                                break;
                            }
                            if ((long)client.Spells[spellIdFromItem].Souls == 7)
                            {
                                client.Inventory.Remove(item, Enums.ItemUse.Remove);
                                client.RemoveSpell(Npcs.LearnableSpell(spellIdFromItem));
                                client.AddSpell(new Spell(true) { ID = spellIdFromItem, Souls = Spell.Soul_Level.Level_Three });
                                break;
                            }
                            else if ((long)client.Spells[spellIdFromItem].Souls == 15)
                            {
                                client.Send(new Message("Sorry you have skill soul in this spell", Color.Red, Message.System));
                                break;
                            }
                            client.Send(new Message("Sorry you need open SkillSoul Level 2 and open this book", Color.Red, Message.System));
                        }
                        else
                        {
                            client.Send(new Message("Sorry you not join JingHu", Color.Red, Message.System));
                        }

                        break;
                    }
                case 3003312:
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            ushort spellIdFromItem = ItemHandler.GetSpellIdFromItem(item.ID);
                            if (!client.Spells.ContainsKey(spellIdFromItem))
                            {
                                client.Send(new Message("Sorry you don't have this spell", Color.Red, Message.System));
                                break;
                            }
                            if ((long)client.Spells[spellIdFromItem].Souls == 15)
                            {
                                client.Inventory.Remove(item, Enums.ItemUse.Remove);
                                client.RemoveSpell(Npcs.LearnableSpell(spellIdFromItem));
                                client.AddSpell(new Spell(true) { ID = spellIdFromItem, Souls = Spell.Soul_Level.Level_Four });
                                break;
                            }
                            else if ((long)client.Spells[spellIdFromItem].Souls == 31)
                            {
                                client.Send(new Message("Sorry you have SkillSoul in this spell", Color.Red, Message.System));
                                break;
                            }
                            client.Send(new Message("Sorry you need open SkillSoul Level 3 and open this book", Color.Red, Message.System));
                        }
                        else
                        {
                            client.Send(new Message("Sorry you not join JingHu", Color.Red, Message.System));
                        }

                        break;
                    }
                case 3003227:
                    {
                        if (client.Entity.MyJiang != null)
                        {
                            ushort spellIdFromItem = ItemHandler.GetSpellIdFromItem(item.ID);
                            if (!client.Spells.ContainsKey(spellIdFromItem))
                            {
                                client.Send(new Message("Sorry you don't have this spell", Color.Red, Message.System));
                                break;
                            }
                            if ((long)client.Spells[spellIdFromItem].Souls == 1)
                            {
                                client.Inventory.Remove(item, Enums.ItemUse.Remove);
                                client.RemoveSpell(Npcs.LearnableSpell(spellIdFromItem));
                                client.AddSpell(new Spell(true) { ID = spellIdFromItem, Souls = Spell.Soul_Level.Level_One });
                                break;
                            }
                            else if ((long)client.Spells[spellIdFromItem].Souls == 3)
                            {
                                client.Send(new Message("Sorry you have SkillSoul in this spell", Color.Red, Message.System));
                                break;
                            }
                        }
                        else
                        {
                            client.Send(new Message("Sorry you not join JingHu", Color.Red, Message.System));
                        }

                        break;
                    }
                #endregion
                #region StatueMaker
                case 720020:
                    {
                        client.Clear = item;
                        client.Send(Data.Custom(Data.CustomCommands.StatueMaker, client.Entity.UID));
                        break;
                    }
                #endregion
                #region MemoryAgate
                case 720828:
                    {
                        item.SendAgate(client);
                        break;
                    }
                #endregion
                #endregion
                #region DemonBoxs
                case 3000277:
                    {
                        if (client.Inventory.Count > 40 - 12)
                        {
                            client.Send("You need atleast 12 open spots!");
                            break;
                        }
                        client.Inventory.Add(720650, 0, 11);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 3000278:
                    {
                        if (client.Inventory.Count > 40 - 12)
                        {
                            client.Send("You need atleast 12 open spots!");
                            break;
                        }
                        client.Inventory.Add(720651, 0, 11);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 3000279:
                    {
                        if (client.Inventory.Count > 40 - 12)
                        {
                            client.Send("You need atleast 12 open spots!");
                            break;
                        }
                        client.Inventory.Add(720652, 0, 11);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 3000280:
                    {
                        if (client.Inventory.Count > 40 - 12)
                        {
                            client.Send("You need atleast 12 open spots!");
                            break;
                        }
                        client.Inventory.Add(720671, 0, 11);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 3000281:
                    {
                        if (client.Inventory.Count > 40 - 12)
                        {
                            client.Send("You need atleast 12 open spots!");
                            break;
                        }
                        client.Inventory.Add(720672, 0, 11);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720650:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2420, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720651:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2421, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720652:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2422, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720671:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2423, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720672:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2424, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                case 720673:
                    {
                        if (client.Entity.MapID == 1036)
                        {
                            client.Send(new Message("Sorry can't open here", Message.System));
                            return;
                        }
                        Database.MonsterInformation mob;
                        Database.MonsterInformation.MonsterInformations.TryGetValue(2425, out mob);
                        client.Map.SpawnMonsterNearToHero(mob, client);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                #endregion
                #region Medicine
                case 1000000:
                case 1000010:
                case 1000020:
                case 1000030:
                case 1002000:
                case 1002010:
                case 1002020:
                case 1000040:
                case 1002050:
                case 725065:
                    throw new InvalidOperationException("Healing medicine must use the owned service.");
                case 1001000:
                case 1001010:
                case 1001020:
                case 1001030:
                case 1001040:
                case 1002030:
                case 1002040:
                case 725066:
                    throw new InvalidOperationException("Healing medicine must use the owned service.");
                #endregion
                #region Gates
                case 1060020:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1002, 408, 330);
                        break;
                    }
                case 1060021:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1000, 500, 650);
                        break;
                    }
                case 1060022:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1020, 565, 562);
                        break;
                    }
                case 1060023:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1011, 188, 264);
                        break;
                    }
                case 1060024:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1015, 717, 571);
                        break;
                    }
                case 1060039:
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Cursed) || client.Entity.MapID == 3846 || client.Entity.MapID == 2068)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            return;
                        }
                        if (client.Entity.MapID == 601)
                        {
                            return;
                        }

                        if (client.Map.BaseID == 6000 || client.Map.BaseID == 6001 || client.Map.BaseID == 6004 || client.Map.BaseID == 6003 || client.Map.BaseID == 10360 || client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 && client.Entity.MapID != 1005 || client.Map.BaseID == 700)
                        {
                            client.Send(Constants.JailItemUnusable);
                            return;
                        }
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Teleport(1217, 535, 558);
                        break;
                    }
                #endregion
                #region Misc
                #region KingdomMissionItems
                case 3007108:
                case 3007109:
                case 3007110:
                    Game.Features.PotencyPacks.Use(client, item);
                    break;
                #endregion+
                #region GoldBrick
                case 3003498:
                    {
                        if (client.Union != null)
                        {
                            client.Union.GoldBricks += 1;
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        }
                        else
                        {
                            client.MessageBox("Please join a Union and try again to use this.");
                        }
                        break;
                    }
                #endregion
                #region CajaDeLuna
                case 3001405:
                    {
                        client.Inventory.Add(721020, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        break;
                    }
                #endregion
                #region LuckyBox1
                case 722450:
                    {
                        client.Entity.Lukypionts += 3;
                        client.Send(new Message("Congratulations" + client.Entity.Name + "You are got 3 Lucky Point.", System.Drawing.Color.DarkOrange, Message.TopLeft));
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Update(_String.Effect, "Lucky", true);
                        Database.EntityTable.UpdateLuckyPoints(client);
                        break;
                    }
                #endregion
                #region LuckyBox2
                case 722150:
                    {
                        client.Entity.Lukypionts += 7;
                        client.Send(new Message("Congratulations" + client.Entity.Name + "You are got 7 Lucky Point.", System.Drawing.Color.DarkGreen, Message.TopLeft));
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Update(_String.Effect, "Lucky", true);
                        Database.EntityTable.UpdateLuckyPoints(client);
                        break;
                    }
                #endregion
                #region LuckyBox3
                case 721853:
                    {
                        client.Entity.Lukypionts += 10;
                        client.Send(new Message("Congratulations" + client.Entity.Name + "You are got 10 Lucky Point.", System.Drawing.Color.Coral, Message.TopLeft));
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Update(_String.Effect, "Lucky", true);
                        Database.EntityTable.UpdateLuckyPoints(client);

                        break;
                    }
                #endregion
                #region LuckyLotteryPack
                case 728305:
                    {
                        if (client.Inventory.Count <= 39)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(711504, 0, 10);
                            client.Inventory.Add(723834, 0, 1);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region Random Accseeoreis
                case 720836:
                    throw new InvalidOperationException("Prize Center pack must use the owned service.");
                #endregion
                #region Random Refinery
                case 723693:
                    {
                        uint ItemID = 0;
                        uint rand = (uint)Kernel.Random.Next(1, 97);
                        switch (rand)
                        {
                            #region Rand Refinery
                            case 1:
                                ItemID = 724350;
                                break;
                            case 2:
                                ItemID = 724351;
                                break;
                            case 3:
                                ItemID = 724352;
                                break;
                            case 4:
                                ItemID = 724353;
                                break;
                            case 5:
                                ItemID = 724354;
                                break;
                            case 6:
                                ItemID = 724355;
                                break;
                            case 7:
                                ItemID = 724356;
                                break;
                            case 8:
                                ItemID = 724357;
                                break;
                            case 9:
                                ItemID = 724358;
                                break;
                            case 10:
                                ItemID = 724359;
                                break;
                            case 11:
                                ItemID = 724360;
                                break;
                            case 12:
                                ItemID = 724361;
                                break;
                            case 13:
                                ItemID = 724362;
                                break;
                            case 14:
                                ItemID = 724350;
                                break;
                            case 15:
                                ItemID = 724363;
                                break;
                            case 16:
                                ItemID = 724364;
                                break;
                            case 17:
                                ItemID = 724365;
                                break;
                            case 18:
                                ItemID = 724366; break;
                            case 19://IceStick 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 2-HandedAccessory A~strange~stick.~Maybe~you~can~play~ice~hockey~with~it. 8 0 0 
                                ItemID = 724367;
                                break;
                            case 20://Wrench 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory It`s~a~heavy~wrench.~Perhaps~you~can~use~it~to~deal~ 8 0 0 
                                ItemID = 724368; break;
                            case 21://WoodenClub 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory This~wooden~club~often~gives~off~a~sweet~odor. 8 0 0 
                                ItemID = 724369; break;
                            case 22://Umbrella 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory An~unbreakable~umbrella~is~a~stealth~weapon~that~also~keeps~you~dry. 8 0 0 
                                ItemID = 724370; break;
                            case 23:////Blowfish 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Dried~in~the~bright~sunlight~over~the~Desert,~the~blowfish~makes~a~good~weapon~for~it`s~as~hard~as~iron. 8 0 0 
                                ItemID = 724371; break;
                            case 24://FeatherDuster 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724372; break;
                            case 25://Spatula 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724373; break;
                            case 26://InvincibleFist 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724374; break;
                            case 27://FishPole 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724375; break;
                            case 28://Pan 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724376; break;
                            case 29://Handbag 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724377;
                                break;
                            case 30://Backpack 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724378;
                                break;
                            case 31://SportsBag 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724379; break;
                            case 32://Bunny 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724380; break;
                            case 33://GoodEveningBear 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724381; break;
                            case 34://Rod 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724382;
                                break;
                            case 35://Clap 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724383;
                                break;
                            case 36://HeavyHammer 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724384;
                                break;
                            case 37://LightSaber 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724385
                                     ;
                                break;
                            case 38://TennisRacket 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724386;
                                break;
                            case 39://ApeCityHam 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory None. 8 0 0 
                                ItemID = 724387;
                                break;
                            case 40: //Wrench 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory It`s~a~heavy~wrench. 8 0 0 
                                ItemID = 724388;
                                break;
                            case 41: //WoodenClub 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory This~wooden~club~often~gives~off~a~sweet~odor. 8 0 0 
                                ItemID = 724389;
                                break;
                            case 42://Umbrella 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory An~unbreakable~umbrella~is~a~stealth~weapon~that~also~keeps~you~dry. 8 0 0 
                                ItemID = 724390;
                                break;
                            case 43://Blowfish 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Dried~in~the~bright~sunlight~over~the~Desert,~the~Mackerel~makes~a~good~weapon~for~it`s~as~hard~as~iron. 8 0 0 
                                ItemID = 724391;
                                break;
                            case 44://FeatherDuster 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724392;
                                break;
                            case 45://Spatula 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724393;
                                break;
                            case 46://InvincibleFist 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724394;
                                break;
                            case 47://FishPole 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724395;
                                break;
                            case 48://Pan 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724396;
                                break;
                            case 49://Handbag 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724397;
                                break;
                            case 50://Backpack 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724398;
                                break;
                            case 51://SportsBag 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724399;
                                break;
                            case 52://Bunny 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724400;
                                break;
                            case 53://GoodEveningBear 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724401;
                                break;
                            case 54://Rod 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724402;
                                break;
                            case 55://Clap 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724403;
                                break;
                            case 56://HeavyHammer 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724404;
                                break;
                            case 57://LightSaber 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724405;
                                break;
                            case 58://TennisRacket 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724406;
                                break;
                            case 59://ApeCityHam 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1-HandedAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724407;
                                break;
                            case 60: //WoodenBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724408;
                                break;
                            case 61://LoveBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724409;
                                break;
                            case 62://SeaHorse 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724410;
                                break;
                            case 63://Harp 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724411;
                                break;
                            case 64://ForceBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724412;
                                break;
                            case 65://WoodenBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724413;
                                break;
                            case 66://LoveBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724414;
                                break;
                            case 67://SeaHorse 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724415;
                                break;
                            case 68://Harp 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724416;
                                break;
                            case 69://ForceBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724417;
                                break;
                            case 70://Wok 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724418;
                                break;
                            case 71://TurtleShell 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724419;
                                break;
                            case 72://LoveShield 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724420;
                                break;
                            case 73://SunFlower 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724421;
                                break;
                            case 74://Wheel 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724422;
                                break;
                            case 75://Wok 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724423;
                                break;
                            case 76://TurtleShell 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724424;
                                break;
                            case 77://LoveShield 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724425;
                                break;
                            case 78://SunFlower 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724423;
                                break;
                            case 79: //WoodenBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724424;
                                break;
                            case 80://LoveBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724425;
                                break;
                            case 81://SeaHorse 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724426;
                                break;
                            case 82://Harp 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724427;
                                break;
                            case 83://ForceBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory None. 8 0 0 
                                ItemID = 724428;
                                break;
                            case 84://WoodenBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724429;
                                break;
                            case 85://LoveBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724430;
                                break;
                            case 86://SeaHorse 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724431;
                                break;
                            case 87://Harp 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724432;
                                break;
                            case 88://ForceBow 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 BowAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724433;
                                break;
                            case 89://Wok 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724434;
                                break;
                            case 90://TurtleShell 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724435;
                                break;
                            case 91://LoveShield 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724436;
                                break;
                            case 92://SunFlower 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724437;
                                break;
                            case 93://Wheel 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 0 0 10080 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory None. 8 0 0 
                                ItemID = 724438;
                                break;
                            case 94://Wok 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724439;
                                break;
                            case 95://TurtleShell 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724440;
                                break;
                            case 96://LoveShield 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724441;
                                break;
                            case 97://SunFlower 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                ItemID = 724442;
                                break;
                            default:
                                ItemID = 724445;
                                break;
                                //Wheel 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 65535 65535 0 0 0 0 0 0 0 0 0 1 800 2 2 1 215 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 ShieldAccessory Right~click~to~equip. 8 0 0 
                                #endregion
                        }
                        client.Inventory.Add(ItemID, 0, 1);
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Delete);
                        break;
                    }
                #endregion
                #region DragonBallPack(5)
                case 720884:
                    throw new InvalidOperationException("DragonBall pack must use the owned service.");
                #endregion
                #region TeratoDragon
                case 720598:
                    client.Send("The dragon's seal is silent. Keep the pill for now.");
                    break;
                #endregion
                #region SnowBanshee
                case 3004259:
                    client.ActiveNpc = 0;
                    client.Send("The Banshee Spirit is silent. Keep it until the summoning rite returns.");
                    break;
                #endregion
                #region PromotionPack
                case 728204:
                    {
                        if (client.Inventory.Count <= 39)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1080001, 0, 1);
                            client.Inventory.Add(721090, 0, 1);
                            client.Inventory.Add(1088001, 0, 1);

                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;

                        //  break;
                    }
                #endregion
                #region UltraStonePack
                case 727991:
                    {
                        if (client.Inventory.Count <= 20)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(730008, 0, 1, item.Bound);
                            client.Inventory.Add(730005, 0, 2, item.Bound);

                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;

                        //  break;
                    }
                #endregion
                #region +3StonePack
                case 729023:
                #endregion
                #region +2StonePack
                case 729022:
                    throw new InvalidOperationException("Higher stone pack must use the owned service.");
                #endregion
                #region 10ExpBallPack
                case 720757:
                    throw new InvalidOperationException("Fixed output pack must use the owned service.");
                #endregion
                #region Meteor Box
                case 723268:
                    throw new InvalidOperationException("Fixed output pack must use the owned service.");
                #endregion
                #region Fireworks
                case 720030:
                    {
                        if (client.Entity.Stamina >= 30)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Entity.Stamina -= 30;
                            ItemUsage data = new ItemUsage(true);
                            data.UID = client.Entity.UID;
                            data.Action = ItemUsage.Mode.FireWorks;
                            data.dwParam = 255;
                            client.Send(data);
                            client.SendScreen(data, true);
                            client.Screen.Reload(null);
                            client.Entity.SendSpawn(client);

                        }
                        break;
                    }
                case 720031:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Update(_String.Effect, "firework-2love", true);
                        break;
                    }
                case 720032:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.Update(_String.Effect, "firework-1love", true);
                        break;
                    }
                #endregion
                #region SmallLotteryTicketPack
                case 724002:
                    throw new InvalidOperationException("Fixed output pack must use the owned service.");
                #endregion
                #region Surprise-Box
                case 722178:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            //client.Inventory.Add(722057, 0, 1);//PowerEXPBall
                            client.Entity.AddConquerPoints(2000);
                        }
                        else
                        {
                            client.Send(new Message("You Must have 1 space in you Inventory To Open You Box", Color.White, 255));
                        }
                        break;
                    }
                #endregion
                #region SteedPacks
                case 728525:
                case 728526:
                case 728527:
                    {
                        Npcs dialog = new Npcs(client);
                        dialog.Text("Hello. You can chose a horse: black, brown or white.");
                        dialog.Option("Black horse", 1);
                        dialog.Option("Brown horse", 2);
                        dialog.Option("White horse", 3);
                        dialog.Option("Nevermind.", 255);
                        dialog.Avatar(96);
                        dialog.Send();
                        client.ActiveNpc = item.ID;
                        break;
                    }
                case 723855:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 1;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 << 8 | 255 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723856:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 1;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 | 255 << 8;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723859:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 1;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 255 | 150 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723860:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 3;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 << 8 | 255 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723861:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 3;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 | 255 << 8;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723862:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 3;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 255 | 150 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723863:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 6;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 << 8 | 255 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723864:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 6;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 | 255 << 8;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723865:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 6;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 255 | 150 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723900:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 0;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 << 8 | 255 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723901:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 0;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 150 | 255 << 8;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                case 723902:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        ConquerItem _item = new ConquerItem(true);
                        _item.ID = 300000;
                        Database.ConquerItemInformation _iteminfos = new Database.ConquerItemInformation(_item.ID, 0);
                        _item.Durability = _item.MaximDurability = _iteminfos.BaseInformation.Durability;
                        _item.Plus = 0;
                        _item.Effect = Game.Enums.ItemEffect.Horse;
                        _item.SocketProgress = 255 | 150 << 16;
                        client.Inventory.Add(_item, Game.Enums.ItemUse.CreateAndAdd);
                        break;
                    }
                #endregion
                #region LifeFruitBasket
                case 723725:
                    {
                        if (client.Inventory.Count <= 31)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(723726, 0, 10);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region 7StarPouch
                case 725067:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(725065, 0, 5);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region SerenityPouch
                case 725068:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(725066, 0, 5);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region SpeedArrowPack
                case 727000:
                    {
                        if (client.Inventory.Count <= 35)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1050002, 0, 1, item.Bound);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region LifeFruit
                //case 723726:
                //    {
                //        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                //        client.Player.Hitpoints = client.Player.MaxHitpoints;
                //        client.Player.Mana = client.Player.MaxMana;
                //        break;
                //    }
                case 723726:
                    throw new InvalidOperationException("Healing medicine must use the owned service.");
                #endregion
                #region Amrita Box
                case 720010:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1000030, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region Panacea Box
                case 720011:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002000, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region Ginseng Box
                case 720012:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002010, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region Vanilla Box
                case 720013:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002020, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region RecoveryPill Box
                case 720014:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1001030, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion RecoveryPill
                #region SoulPill Box
                case 720015:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1001040, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region RefreshingPill Box
                case 720016:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002030, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region ChantPill Box
                case 720017:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002040, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region Mil.Ginseng Box
                case 721330:
                    {
                        if (client.Inventory.Count <= 38)
                        {
                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            client.Inventory.Add(1002050, 0, 3);
                        }
                        else
                        {
                            client.Send(Constants.FullInventory);
                        }

                        break;
                    }
                #endregion
                #region MeteorTearScroll
                case 723711:
                    throw new InvalidOperationException("Fixed output pack must use the owned service.");
                #endregion
                #region +1Stone Pack
                case 723712:
                    throw new InvalidOperationException("+1 stone pack must use the owned service.");
                #endregion



                #region DrasgonBallScroll
                case 720028:
                    throw new InvalidOperationException("DragonBall pack must use the owned service.");
                #endregion
                #region Subclass study books
                case 723340:
                case 723341:
                case 728883:
                case 723342:
                    Revora.Game.Features.SubclassShop.ReadBook(client, item);
                    break;
                #endregion
                #region StudyBook[Arena]
                case 720774:
                    throw new InvalidOperationException("Study book must use the owned service.");
                #endregion
                #region StudyBook[Race]
                case 720775:
                    throw new InvalidOperationException("Study book must use the owned service.");
                #endregion
                #region ExperiencePotion
                case 3002559:
                    throw new InvalidOperationException("EXP protection pill must use the owned service.");
                case 720793:
                case 720981:
                case 728201:
                    throw new InvalidOperationException("Tournament EXP pack must use the owned service.");
                #endregion
                #region TimeDropSlayer x Horas
                case 3007992:
                case 3007989:
                case 3007985:
                    throw new InvalidOperationException("TimeDrop hourglass must use the owned service.");
                #endregion
                #region NinjaAmulet
                case 723583:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        switch (client.Entity.Body % 10)
                        {
                            case 2:
                            case 4:
                                client.Entity.Body--;
                                break;
                            case 1:
                            case 3:
                                client.Entity.Body++;
                                break;
                        }
                        break;
                    }
                #endregion
                #region DisguiseAmulet
                case 723724:
                    {
                        int disguise = Kernel.Random.Next(Database.DataHolder.Disguises.Length);
                        ushort selected = Database.DataHolder.Disguises[disguise];
                        client.Inventory.Remove(item, Enums.ItemUse.Remove);
                        bool wasTransformated = client.Entity.Transformed;
                        if (wasTransformated)
                        {
                            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                            client.Entity.TransformationID = 0;
                            client.Entity.TransformationStamp = Time32.Now;
                        }
                        ushort transformation = selected;
                        client.Entity.TransformationID = transformation;
                        client.Entity.TransformationStamp = Time32.Now;
                        client.Entity.TransformationTime = 110;
                        SpellUse spellUse = new SpellUse(true);
                        spellUse.Attacker = client.Entity.UID;
                        spellUse.SpellID = 1360;
                        spellUse.SpellLevel = 4;
                        spellUse.X = client.Entity.X;
                        spellUse.Y = client.Entity.Y;
                        spellUse.AddTarget(client.Entity.UID, (uint)0, null);
                        client.Send(spellUse);
                        client.Entity.TransformationMaxHP = 3000;
                        double maxHP = client.Entity.MaxHitpoints;
                        double HP = client.Entity.Hitpoints;
                        double point = HP / maxHP;
                        client.Entity.Hitpoints = (uint)(client.Entity.TransformationMaxHP * point);
                        client.Entity.Update((byte)FlagsUpdate.DataType.MaxHitpoints, client.Entity.TransformationMaxHP, false);
                        break;
                    }
                #endregion
                #region +8StonePack
                case 721222:
                    {
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 38)
                            {
                                client.Inventory.Add(730008, 8, 1, item.Bound);
                                client.Inventory.Add(730006, 6, 1, item.Bound);
                                client.Inventory.Remove(item, Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 2 free spots in your inventory.", Message.System));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", Message.System));
                        }

                        break;
                    }
                #endregion
                #region ToughDrillPack
                case 721223:
                    {
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 38)
                            {
                                client.Inventory.Add(1200005, 0, 1, item.Bound);//ToughDrill
                                client.Inventory.Add(730003, 3, 1, item.Bound); //+3
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 2 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperWarrior`sArmorPack
                case 721224:
                    {
                        uint itemid = 131069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperWarrior`sHelmetPack
                case 721225:
                    {
                        uint itemid = 111069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTrojan`sArmorPack
                case 721226:
                    {
                        uint itemid = 130069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTrojan`sCoronetPack
                case 721227:
                    {
                        uint itemid = 118069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperNinja`sVestPack
                case 721228:
                    {
                        uint itemid = 135069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperNinja`sVeilPack
                case 721229:
                    {
                        uint itemid = 112069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTaoist`sRobePack
                case 721230:
                    {
                        uint itemid = 134069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTaoist`sCapPack
                case 721231:
                    {
                        uint itemid = 114069;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperArcher`sCoatPack
                case 721232:
                    {
                        uint itemid = 133049;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperArcher`sHatPack
                case 721233:
                    {
                        uint itemid = 113049;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperNecklacePack
                case 721234:
                    {
                        uint itemid = 120129;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperRingPack
                case 721235:
                    {
                        uint itemid = 150139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTaoist`sBagPack
                case 721236:
                    {
                        uint itemid = 121129;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperTaoist`sBraceletPack
                case 721237:
                    {
                        uint itemid = 152149;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperBootsPack
                case 721238:
                    {
                        uint itemid = 160139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperBladePack
                case 721239:
                    {
                        uint itemid = 410139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperSwordPack
                case 721240:
                    {
                        uint itemid = 420139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperClubPack
                case 721241:
                    {
                        uint itemid = 480139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperShieldPack
                case 721242:
                    {
                        uint itemid = 900049;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperKatanaPack
                case 721243:
                    {
                        uint itemid = 601139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperBackswordPack
                case 721244:
                    {
                        uint itemid = 421139;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region SuperBowPack
                case 721245:
                    {
                        uint itemid = 500129;
                        if (client.Entity.Level >= 10)
                        {
                            if (client.Inventory.Count < 32)
                            {
                                client.Inventory.Add(1200000, 0, 1, item.Bound);//PrayingStone
                                client.Inventory.Add(723700, 0, 1, item.Bound);//ExpBall
                                client.Inventory.Add(723017, 0, 5, item.Bound); //ExpPotion
                                ConquerItem items = new ConquerItem(true);
                                items.ID = itemid;
                                items.Bound = item.Bound;
                                items.Color = Game.Enums.Color.White;
                                items.Plus = 5;
                                items.SocketOne = Game.Enums.Gem.EmptySocket;
                                items.Durability = items.MaximDurability = Database.ConquerItemInformation.BaseInformations[itemid].Durability;
                                client.Inventory.Add(items, Game.Enums.ItemUse.CreateAndAdd);
                                client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                            }
                            else
                            {
                                client.Send(new Message("You need to make atleast 8 free spots in your inventory.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                        }
                        else
                        {
                            client.Send(new Message("You must be atleast level 10 to open the Pack", System.Drawing.Color.Red, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #endregion
                #region Habilidades [Tienda]100%
                #region Trueno
                case 725000:
                    {
                        if (client.Entity.Spirit >= 20)
                        {
                            client.Inventory.Remove(725000, 1);
                            client.AddSpell(new Spell(true) { ID = 1000 });
                        }
                        else
                        {
                            client.Send(new Message("Nesecitas 20 de Espirituo para aprenderla!", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region TruenoDeFuego
                case 725001:
                    {
                        if (client.Entity.Spirit >= 80)
                        {
                            client.Inventory.Remove(725001, 1);
                            client.AddSpell(new Spell(true) { ID = 1001 });
                        }
                        else
                        {
                            client.Send(new Message("Nesecitas 80 de Espirituo para aprenderla!", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Tornado
                case 725002:
                    {
                        if (client.Entity.Class >= 140 && client.Entity.Class <= 145 && client.Entity.Level >= 90 && client.Entity.Spirit >= 160)
                        {
                            client.Inventory.Remove(725002, 1);
                            client.AddSpell(new Spell(true) { ID = 1002 });
                        }
                        else
                        {
                            client.Send(new Message("Only Fire Taoists of level 90 or above, with 160 Spirit, may learn this skill!", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Tratamiento
                case 725003:
                    {
                        if (client.Entity.Spirit >= 30)
                        {
                            client.Inventory.Remove(725003, 1);
                            client.AddSpell(new Spell(true) { ID = 1005 });
                        }
                        else
                        {
                            client.Send(new Message("Nesecitas 30 de Espirituo para aprenderla!", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Relampago [XP]
                case 725004:
                    {
                        if (client.Entity.Class >= 130 && client.Entity.Class <= 135 || client.Entity.Class >= 140 && client.Entity.Class <= 145 && client.Entity.Level >= 15 || client.Entity.Class == 100 || client.Entity.Class == 101)
                        {
                            client.Inventory.Remove(725004, 1);
                            client.AddSpell(new Spell(true) { ID = 1010 });
                        }
                        else
                        {
                            client.Send(new Message("Only Taoists may learn this skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Lanza
                case 721158:
                    {
                        if (client.Entity.Level >= 40)
                        {
                            for (int i = 0; i < Database.SpellTable.SpellInformations[11005][0].WeaponSubtype.Count; i++)
                            {

                                var prof = Database.SpellTable.SpellInformations[11005][0].WeaponSubtype[i];
                                if (client.Proficiencies.ContainsKey(prof))
                                {
                                    if (client.Proficiencies[prof].Level >= 5)
                                    {
                                        client.Inventory.Remove(721158, 1);
                                        client.AddSpell(new Spell(true) { ID = 11005 });
                                        return;
                                    }
                                }
                            }
                            client.Send(new Message("Debes tener Requerimiento de lanza Nivel 5, nivel 40", System.Drawing.Color.Tan, Message.TopLeft));

                        }
                        break;
                    }
                #endregion
                #region RelampagoDeCielo
                case 721157:
                    {
                        if (client.Entity.Level >= 40)
                        {
                            for (int i = 0; i < Database.SpellTable.SpellInformations[11000][0].WeaponSubtype.Count; i++)
                            {

                                var prof = Database.SpellTable.SpellInformations[11000][0].WeaponSubtype[i];
                                if (client.Proficiencies.ContainsKey(prof))
                                {
                                    if (client.Proficiencies[prof].Level >= 5)
                                    {
                                        client.Inventory.Remove(721157, 1);
                                        client.AddSpell(new Spell(true) { ID = 11000 });
                                    }
                                }
                            }
                            client.Send(new Message("Debes tener Requerimiento de RelampagoDeCielo Nivel 5, nivel 140", System.Drawing.Color.Tan, Message.TopLeft));

                        }
                        break;
                    }
                #endregion                
                #region CorazonDivino[Liebre]
                case 725015:
                    {
                        if (client.Entity.Class >= 130 && client.Entity.Class <= 135)
                        {
                            client.Inventory.Remove(725015, 1);
                            client.AddSpell(new Spell(true) { ID = 1350 });
                        }
                        else
                        {
                            client.Send(new Message("Only Taoists may learn this skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        }

                        break;
                    }
                #endregion
                #region DiabloDeNoche [XP]
                case 725016:
                    {
                        client.Inventory.Remove(725016, 1);
                        client.AddSpell(new Spell(true) { ID = 1360 });
                        client.Send(new Message("You have learned Night Devil, an XP skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile2
                case 725018:
                    {
                        client.Inventory.Remove(725018, 1);
                        client.AddSpell(new Spell(true) { ID = 1380 });
                        client.Send(new Message("You have learned Dance2", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile3
                case 725019:
                    {
                        client.Inventory.Remove(725019, 1);
                        client.AddSpell(new Spell(true) { ID = 1385 });
                        client.Send(new Message("You have learned Dance3", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile4
                case 725020:
                    {
                        client.Inventory.Remove(725020, 1);
                        client.AddSpell(new Spell(true) { ID = 1390 });
                        client.Send(new Message("You have learned Dance4", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile5
                case 725021:
                    {
                        client.Inventory.Remove(725021, 1);
                        client.AddSpell(new Spell(true) { ID = 1395 });
                        client.Send(new Message("You have learned Dance5", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile6
                case 725022:
                    {
                        client.Inventory.Remove(725022, 1);
                        client.AddSpell(new Spell(true) { ID = 1400 });
                        client.Send(new Message("You have learned Dance6", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile7
                case 725023:
                    {
                        client.Inventory.Remove(725023, 1);
                        client.AddSpell(new Spell(true) { ID = 1405 });
                        client.Send(new Message("You have learned Dance7", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region Baile8
                case 725024:
                    {
                        client.Inventory.Remove(725024, 1);
                        client.AddSpell(new Spell(true) { ID = 1410 });
                        client.Send(new Message("You have learned Dance8", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region CorteDeLunar [xp]
                case 725025:
                    {
                        client.Inventory.Remove(725023, 1);
                        client.AddSpell(new Spell(true) { ID = 1320 });
                        client.Send(new Message("You have learned Moon Cut, an XP skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        break;
                    }
                #endregion
                #region  Relampago Rapido [xp]
                case 725028:
                    {
                        if (client.Entity.Class >= 130 && client.Entity.Class <= 135 || client.Entity.Class >= 140 && client.Entity.Class <= 145)
                        {
                            client.AddSpell(new Spell(true) { ID = 5001 });
                            client.Inventory.Remove(725028, 1);
                            client.Send(new Message("You have learned Fast Lightning, an XP skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        else
                        {
                            client.Send(new Message("Only Taoists may learn this kind of skill!", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Bomba [Taoistas]
                case 1060100:
                    {
                        if (client.Entity.Class >= 140 && client.Entity.Class <= 145 && client.Entity.Level >= 140)
                        {
                            client.Inventory.Remove(1060100, 1);
                            client.AddSpell(new Spell(true) { ID = 1160 });
                            client.Send(new Message("You have learned Bomb for Taoists.", System.Drawing.Color.Tan, Message.TopLeft));

                        }
                        else
                        {
                            client.Send(new Message("Only Taoists of level 140 may learn this skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #region Fuego Infernal[Taoista VIP]
                case 1060101:
                    {
                        if (client.Entity.Class >= 140 && client.Entity.Class <= 145 && client.Entity.Level >= 140 && client.Entity.VIPLevel >= 6)
                        {
                            client.Inventory.Remove(1060101, 1);
                            client.AddSpell(new Spell(true) { ID = 1165 });
                            client.Send(new Message("You have learned Hell Fire for VIP Taoists.", System.Drawing.Color.Tan, Message.TopLeft));

                        }
                        else
                        {
                            client.Send(new Message("Only VIP 6 Taoists of level 140 may learn this skill.", System.Drawing.Color.Tan, Message.TopLeft));
                        }
                        break;
                    }
                #endregion
                #endregion
                #region Refinery Packs
                #region Penetration - Bracelet
                case 723130:
                case 723131:
                case 723132:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Penetration - Headgear
                case 723684:
                case 723685:
                case 723686:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Penetration - Bag
                case 724149:
                case 724150:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724148:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 237;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                #region Skill Critical Strike - Backsword
                case 723133:
                case 723134:
                case 723135:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Skill Critical Strike - Bracelet
                case 724196:
                case 724197:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724195:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 230;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                #region Block - Shield
                case 723669:
                case 723670:
                case 723671:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Block - Headgear
                case 724145:
                case 724146:
                case 724147:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion

                #region CriticalStrike - Bow
                case 723651:
                case 723652:
                case 723653:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region CriticalStrike - 1 Handed weapons
                case 723654:
                case 723655:
                case 723656:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region CriticalStrike - 2 Handed weapons
                case 723657:
                case 723658:
                case 723659:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region CriticalStrike - 2 Ring
                case 724215:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 200;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                case 724193:
                case 724194:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                #endregion

                #region Detoxication - Necklace
                case 723682:
                case 723683:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 723681:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 667;
                        if (newItemID == 724348 || newItemID == 724349)
                        {
                            newItemID += 150;
                        }

                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region Detoxication - Headgear
                case 723672:
                case 723673:
                case 723674:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Detoxication - Bag
                case 723691:
                case 723692:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 723690:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 683;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region Detoxication - Armor
                case 723675:
                case 723676:
                case 723677:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Detoxication - Boots
                case 723679:
                case 723680:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 723678:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 815;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                #region Breakthrough - 1 Handed weapons
                case 723660:
                case 723661:
                case 723662:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Breakthrough - 2 Handed weapons
                case 723663:
                case 723664:
                case 723665:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Breakthrough - Bracelet
                case 724202:
                case 724203:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724201:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 162;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region Breakthrough - Bow
                case 723666:
                case 723667:
                case 723668:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Breakthrough - Ring
                case 724199:
                case 724200:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724198:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 266;
                        if (newItemID >= 724466)
                        {
                            newItemID += 4;
                        }

                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                #region Counteraction - Armor
                case 724142:
                case 724143:
                case 724144:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Counteraction - Bag
                case 724208:
                case 724209:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724207:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 161;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region Counteraction - Necklace
                case 724205:
                case 724206:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724204:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 274;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                #region Immunity - Boots
                case 724211:
                case 724212:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 724210:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 225;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region Immunity - Armor
                case 724136:
                case 724137:
                case 724138:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion
                #region Intensification - Headgear
                case 724139:
                case 724140:
                case 724141:
                    throw new InvalidOperationException("Power Arena pack must use the owned service.");
                #endregion

                #region M-Defense - Necklace
                case 725161:
                case 725162:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 725160:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 32;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region M-Defense - Bag
                case 725164:
                case 725165:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 725163:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 34;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region M-Defense - Bracelet
                case 725167:
                case 725168:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 725166:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 36;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion
                #region M-Defense - Ring
                case 725170:
                case 725171:
                    throw new InvalidOperationException("Honor refinery pack must use the owned service.");
                case 725169:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        byte extra = GetNextRefineryItem();
                        uint newItemID = item.ID + extra + 38;
                        if (item.Bound)
                        {
                            client.Inventory.AddBound(newItemID, 0, 1);
                        }
                        else
                        {
                            client.Inventory.Add(newItemID, 0, 1);
                        }

                        break;
                    }
                #endregion

                case 724131:
                case 724132:
                case 724134:
                case 724135:
                case 724152:
                case 724153:
                case 724155:
                case 724156:
                case 724158:
                case 724159:
                case 724161:
                case 724162:
                case 724164:
                case 724165:
                case 724167:
                case 724168:
                case 724170:
                case 724171:
                case 724173:
                case 724174:
                case 724176:
                case 724177:
                case 724179:
                case 724180:
                case 724182:
                case 724183:
                case 724185:
                case 724186:
                case 724191:
                case 724192:
                    throw new InvalidOperationException("Gain refinery pack must use the owned service.");
                case 724130:
                case 724133:
                case 724151:
                case 724154:
                case 724157:
                case 724160:
                case 724163:
                case 724166:
                case 724169:
                case 724172:
                case 724175:
                case 724178:
                case 724181:
                case 724184:
                case 724190:
                case 725055:
                case 725056:
                case 725057:
                case 725058: GainRefineryItem(item, client); break;
                #endregion
                #region DragonSoul
                #region Box Soul Super
                case 721507:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random Rand = new Random();
                        int dis = Rand.Next(1, 35);
                        if (dis == 1) { client.Inventory.Add(822052, 0, 1); }
                        if (dis == 2) { client.Inventory.Add(822053, 0, 1); }
                        if (dis == 3) { client.Inventory.Add(822055, 0, 1); }
                        if (dis == 4) { client.Inventory.Add(800413, 0, 1); }
                        if (dis == 5) { client.Inventory.Add(800014, 0, 1); }
                        if (dis == 6) { client.Inventory.Add(800015, 0, 1); }
                        if (dis == 7) { client.Inventory.Add(800014, 0, 1); }
                        if (dis == 8) { client.Inventory.Add(800016, 0, 1); }
                        if (dis == 9) { client.Inventory.Add(800512, 0, 1); }
                        if (dis == 10) { client.Inventory.Add(800613, 0, 1); }
                        if (dis == 11) { client.Inventory.Add(800415, 0, 1); }
                        if (dis == 12) { client.Inventory.Add(800420, 0, 1); }
                        if (dis == 13) { client.Inventory.Add(800513, 0, 1); }
                        if (dis == 14) { client.Inventory.Add(800017, 0, 1); }
                        if (dis == 15) { client.Inventory.Add(820052, 0, 1); }
                        if (dis == 16) { client.Inventory.Add(820053, 0, 1); }
                        if (dis == 17) { client.Inventory.Add(820054, 0, 1); }
                        if (dis == 18) { client.Inventory.Add(820055, 0, 1); }
                        if (dis == 19) { client.Inventory.Add(820056, 0, 1); }
                        if (dis == 20) { client.Inventory.Add(820057, 0, 1); }
                        if (dis == 21) { client.Inventory.Add(823055, 0, 1); }
                        if (dis == 22) { client.Inventory.Add(824017, 0, 1); }
                        if (dis == 23) { client.Inventory.Add(821031, 0, 1); }
                        if (dis == 24) { client.Inventory.Add(821032, 0, 1); }
                        if (dis == 25) { client.Inventory.Add(820071, 0, 1); }
                        if (dis == 26) { client.Inventory.Add(820072, 0, 1); }
                        if (dis == 27) { client.Inventory.Add(800722, 0, 1); }
                        if (dis == 28) { client.Inventory.Add(800804, 0, 1); }
                        if (dis == 29) { client.Inventory.Add(800809, 0, 1); }
                        if (dis == 30) { client.Inventory.Add(823056, 0, 1); }
                        if (dis == 31) { client.Inventory.Add(800254, 0, 1); }
                        if (dis == 32) { client.Inventory.Add(800253, 0, 1); }
                        if (dis == 33) { client.Inventory.Add(800421, 0, 1); }
                        if (dis == 34) { client.Inventory.Add(800110, 0, 1); }
                        if (dis == 35) { client.Inventory.Add(800320, 0, 1); }
                    }
                    break;
                #endregion
                #region Box Soul elite
                case 721508:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random Rand = new Random();
                        int dis = Rand.Next(1, 17);
                        if (dis == 1) { client.Inventory.Add(823041, 0, 1); }
                        if (dis == 2) { client.Inventory.Add(820053, 0, 1); }
                        if (dis == 3) { client.Inventory.Add(823043, 0, 1); }
                        if (dis == 4) { client.Inventory.Add(822056, 0, 1); }
                        if (dis == 5) { client.Inventory.Add(800723, 0, 1); }
                        if (dis == 6) { client.Inventory.Add(823045, 0, 1); }
                        if (dis == 7) { client.Inventory.Add(800414, 0, 1); }
                        if (dis == 8) { client.Inventory.Add(800070, 0, 1); }
                        if (dis == 9) { client.Inventory.Add(800050, 0, 1); }
                        if (dis == 10) { client.Inventory.Add(800200, 0, 1); }
                        if (dis == 11) { client.Inventory.Add(800230, 0, 1); }
                        if (dis == 12) { client.Inventory.Add(800420, 0, 1); }
                        if (dis == 13) { client.Inventory.Add(800520, 0, 1); }
                        if (dis == 14) { client.Inventory.Add(800140, 0, 1); }
                        if (dis == 15) { client.Inventory.Add(822057, 0, 1); }
                        if (dis == 16) { client.Inventory.Add(800141, 0, 1); }
                        break;
                    }
                #endregion
                #region Box Soul unique
                case 721509:
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        Random Rand = new Random();
                        int dis = Rand.Next(1, 7);
                        if (dis == 1) { client.Inventory.Add(151219, 0, 1); }
                        if (dis == 2) { client.Inventory.Add(822052, 0, 1); }
                        if (dis == 3) { client.Inventory.Add(820054, 0, 1); }
                        if (dis == 4) { client.Inventory.Add(822054, 0, 1); }
                        if (dis == 5) { client.Inventory.Add(823047, 0, 1); }
                        if (dis == 6) { client.Inventory.Add(823049, 0, 1); }
                        if (dis == 7) { client.Inventory.Add(800420, 0, 1); }
                        break;
                    }
                #endregion
                #endregion
                #region Paquete de Bienvenida
                #region BeginnerPackL1
                case 723753:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion
                #region BeginnerPackL10
                case 723755:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion
                #region BeginnerPackL70
                case 723767:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion
                #region BeginnerPackL100
                case 723772:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion
                #region BeginnerPackL110
                case 723774:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion
                #region BeginnerPackL120
                case 723776:
                    throw new InvalidOperationException("Welcome pack must use the owned service.");
                #endregion

                #region BeginnerPackL1
                case 727026:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL5
                case 727027:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL10
                case 727028:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL15
                case 727029:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL20
                case 727030:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL25
                case 727031:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL30
                case 727032:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL35
                case 727033:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL40
                case 727034:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL45
                case 727035:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL50
                case 727036:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL55
                case 727037:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL60
                case 727038:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL65
                case 727039:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL70
                case 727040:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL75
                case 727041:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL80
                case 727042:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL85
                case 727043:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL90
                case 727044:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL95
                case 727045:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL100
                case 727046:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL105
                case 727047:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL110
                case 727048:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL115
                case 727049:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #region BeginnerPackL120
                case 727050:
                    throw new InvalidOperationException("Starter pack must use the owned service.");
                #endregion
                #endregion
                #region AttackPot & DefensePot
                case 721627:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                case 721672:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                case 721623:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                case 721628:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                case 721673:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                case 721624:
                    throw new InvalidOperationException("Attack defense potion must use the owned service.");
                    #endregion
            }
            client.Entity.UseItem = false;
            client.SpiltStack = false;
        }
        public static byte GetNextRefineryItem()
        {
            if (Kernel.Rate(10))
            {
                return 2;
            }
            else if (Kernel.Rate(30))
            {
                return 1;
            }

            return 0;
        }
        public static void GainRefineryItem(ConquerItem item, GameState c)
        {
            Refinery.RefineryBoxes RefineryB = null;
            if (Kernel.DatabaseRefineryBoxes.TryGetValue(item.ID, out RefineryB))
            {
                List<Refinery.RefineryItem> Possible = new List<Refinery.RefineryItem>();
                foreach (Refinery.RefineryItem RefineryI in Kernel.DatabaseRefinery.Values)
                {
                    if (RefineryI.Type == RefineryB.Type)
                    {
                        if (RefineryI.Position == RefineryB.Position)
                        {
                            if (RefineryI.Untradable == RefineryB.Untradable)
                            {
                                Possible.Add(RefineryI);
                            }
                        }
                    }
                }
                if (Possible.Count > 0)
                {
                    Random Rand = new Random();
                    Int32 x = Rand.Next(1, Possible.Count);
                    Refinery.RefineryItem Refinery = Possible[x];

                    if (Refinery != null)
                    {
                        c.Inventory.Remove(item, Game.Enums.ItemUse.Remove);

                        ConquerItem i = new ConquerItem(true);
                        i.ID = Refinery.Identifier;
                        c.Inventory.Add(i, Game.Enums.ItemUse.CreateAndAdd);
                    }
                }
            }
        }
        public static void PickupItem(FloorItem floorItem, GameState client)
        {
            if (client.Screen.TryGetFloorItem(floorItem.UID, out floorItem) && !client.Trade.InTrade)
            {
                if (Game.Features.MonsterEquipmentDrops.Pickup(client, floorItem)) return;
                if (Game.Features.TeratoRewards.Pickup(client, floorItem)) return;
                if (Game.Features.SnowBansheeRewards.Pickup(client, floorItem)) return;
                if (Game.Features.PurpleBansheeRewards.Pickup(client, floorItem)) return;
                if (Game.Features.SwordMasterRewards.Pickup(client, floorItem)) return;
                if (Game.Features.GrottoKeyDrops.Pickup(client, floorItem)) return;
                if (Game.Features.NinjaFlowerHunt.Pickup(client, floorItem)) return;
                if (Game.Features.CleanWaterDrops.Pickup(client, floorItem)) return;
                if (Game.Features.MeteorDrops.Pickup(client, floorItem)) return;
                if (Game.Features.DailyHuntDrops.Pickup(client, floorItem)) return;
                if (floorItem.DailyBossRun != null)
                {
                    Game.Features.DailyWorldBosses.Pickup(client, floorItem);
                    return;
                }
                if (floorItem.Item != null && floorItem.Item.MobDropped && Game.Features.PotencyPacks.Points(floorItem.Item.ID) != 0)
                {
                    Game.Features.PotencyPacks.Pickup(client, floorItem);
                    return;
                }
                if (floorItem.ValueType == FloorItem.FloorValueType.Money)
                {
                    Game.Features.GoldPickup.TryCollect(client, floorItem, false);
                    return;
                }
                if (client.Entity.X == floorItem.X && client.Entity.Y == floorItem.Y)
                {
                    uint owner = floorItem.Owner == null ? 0 : (uint)floorItem.Owner.Entity.UID;
                    if ((owner != 0 && owner == client.Entity.UID) || owner == 0 || Time32.Now >= floorItem.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter))
                    {
                        goto Jump;
                    }
                    else if (client.Team != null)
                    {
                        if (client.Team.IsTeammate(owner))
                        {
                            if (client.Team.PickupItems && floorItem.ValueType == FloorItem.FloorValueType.Item)
                            {
                                if (floorItem.ItemID != 1088000 && floorItem.ItemID != 1088001)
                                {
                                    goto Jump;
                                }
                            }
                            else if (client.Team.PickupMoney && floorItem.ValueType != FloorItem.FloorValueType.Item)
                            {
                                goto Jump;
                            }
                        }
                    }
                    client.Send(Constants.FloorItemNotAvailable);
                    return;
                Jump:
                    FloorItem pickupAnimation = new FloorItem(true);
                    if (floorItem.ValueType != FloorItem.FloorValueType.Item)
                    {
                        client.Map.RemoveFloorItem(floorItem);
                    }

                    pickupAnimation.Type = 3;
                    pickupAnimation.UID = client.Entity.UID;
                    pickupAnimation.X = client.Entity.X;
                    pickupAnimation.Y = client.Entity.Y;
                    switch (floorItem.ValueType)
                    {
                        case FloorItem.FloorValueType.Item:
                            {
                                if (client.Inventory.Count <= 39)
                                {
                                    client.SendScreen(pickupAnimation, false);
                                    floorItem.Type = 2;
                                    client.RemoveScreenSpawn(floorItem, true);
                                    client.Map.RemoveFloorItem(floorItem);
                                    client.Send(Constants.PickupItem(Database.ConquerItemInformation.BaseInformations[floorItem.Item.ID].Name));
                                    foreach (GameState clients in Server.GamePool)
                                    {
                                        clients.Screen.Remove(floorItem);
                                    }
                                    if (floorItem.Item.MobDropped)
                                    {
                                        client.Inventory.Add(floorItem.Item, Game.Enums.ItemUse.CreateAndAdd);
                                    }
                                    else
                                    {
                                        client.Inventory.Add(floorItem.Item, Game.Enums.ItemUse.Add);
                                    }
                                }
                                else
                                {
                                    client.Send(Constants.FullInventory);
                                }

                                break;
                            }
                        case FloorItem.FloorValueType.ConquerPoints:
                            {
                                client.Send(Constants.PickupConquerPoints(floorItem.Value));
                                client.SendScreen(pickupAnimation, false);
                                client.Entity.AddConquerPoints(floorItem.Value);
                                floorItem.Type = 2;
                                client.RemoveScreenSpawn(floorItem, true);
                                break;
                            }
                        case FloorItem.FloorValueType.BoundCps:
                            {
                                client.Send(Constants.PickupBoundCps(floorItem.Value));
                                client.SendScreen(pickupAnimation, false);
                                client.Entity.BoundCps += floorItem.Value;
                                floorItem.Type = 2;
                                client.RemoveScreenSpawn(floorItem, true);
                                break;
                            }
                    }
                    return;
                }
            }
        }
        public static void DropItem(ItemUsage itemUsage, GameState client)
        {
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemUsage.UID, out item))
            {
                if (Revora.Game.Features.MoonBoxQuest.Material(item.ID)) return;
                if (client.Entity.MapID == (ushort)client.Entity.UID)
                {
                    client.Send(new Message("Do not drop items in your house!", Message.TopLeft));
                    return;
                }
                if (item.ID == 0)
                {
                    return;
                }

                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.ID, 0);
                if (item.Lock != 0 || item.Suspicious)
                {
                    return;
                }

                if (item.ID == 729611 || item.ID == 3001172 || item.ID == 729612 || item.ID == 729613 || item.ID == 729614 || item.ID == 729703)
                {
                    client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    return;
                }
                if (client.Entity.MapID != 1098)
                {
                    if (infos.BaseInformation.Type == Database.ConquerItemBaseInformation.ItemType.Dropable && !item.Bound)
                    {
                        ushort X = client.Entity.X, Y = client.Entity.Y;
                        if (client.Map.SelectCoordonates(ref X, ref Y))
                        {
                            FloorItem floorItem = new FloorItem(true);
                            if (!item.Purification.Available)
                            {
                                floorItem = new FloorItem(true)
                                {
                                    Item = item,
                                    ItemID = item.ID,
                                    ItemColor = item.Color,
                                    MapID = client.Map.ID,
                                    MapObjType = MapObjectType.Item,
                                    X = X,
                                    Y = Y,
                                    Type = 1,
                                    OnFloor = Time32.Now,
                                    UID = FloorItem.FloorUID.Next
                                };
                            }
                            else
                            {
                                floorItem = new FloorItem(true)
                                {
                                    Item = item,
                                    ItemID = item.Purification.PurificationItemID,
                                    MapID = client.Map.ID,
                                    MapObjType = MapObjectType.Item,
                                    X = X,
                                    Y = Y,
                                    Type = 1,
                                    OnFloor = Time32.Now,
                                    UID = FloorItem.FloorUID.Next
                                };
                            }
                            lock (Kernel.Dropitem)
                            {
                                Kernel.Dropitem.Add(item.UID, item);
                            }
                            while (client.Map.Npcs.ContainsKey(floorItem.UID))
                            {
                                floorItem.UID = FloorItem.FloorUID.Next;
                            }

                            client.SendScreenSpawn(floorItem, true);
                            client.Map.AddFloorItem(floorItem);
                            client.Inventory.Dropitem(item, Enums.ItemUse.Remove);
                            Server.AddDropLog(client.Entity.Name, floorItem.Item);
                        }
                    }
                    else
                    {
                        client.Inventory.Remove(item, Enums.ItemUse.Remove);
                    }
                }
                else
                {
                    client.Send(Constants.Dropitemout);
                }
            }
        }
        public static void DropMoney(ItemUsage itemUsage, GameState client)
        {
            if (client.Entity.Money >= itemUsage.UID)
            {
                ushort X = client.Entity.X, Y = client.Entity.Y;
                if (client.Map.SelectCoordonates(ref X, ref Y))
                {
                    uint ItemID = MoneyItemID(itemUsage.UID);
                    FloorItem floorItem = new FloorItem(true);
                    floorItem.ValueType = FloorItem.FloorValueType.Money;
                    floorItem.Value = itemUsage.UID;
                    floorItem.ItemID = ItemID;
                    floorItem.MapID = client.Map.ID;
                    floorItem.MapObjType = Game.MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Y = Y;
                    floorItem.Type = FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.UID = FloorItem.FloorUID.Next;
                    while (client.Map.Npcs.ContainsKey(floorItem.UID))
                    {
                        floorItem.UID = FloorItem.FloorUID.Next;
                    }

                    client.SendScreenSpawn(floorItem, true);
                    client.Map.AddFloorItem(floorItem);
                    client.Entity.AdjustMoney(-(decimal)(itemUsage.UID));

                }
            }
        }
        public static uint MoneyItemID(uint value)
        {
            if (value < 100)
            {
                return 1090000;
            }
            else if (value < 399)
            {
                return 1090010;
            }
            else if (value < 5099)
            {
                return 1090020;
            }
            else if (value < 8099)
            {
                return 1091000;
            }
            else if (value < 12099)
            {
                return 1091010;
            }
            else
            {
                return 1091020;
            }
        }
        public static void HandleBuyFromNPC(ItemUsage itemUsage, GameState client)
        {
            if (itemUsage != null && itemUsage.UID == Database.ChampionShop.UID)
            {
                Revora.Game.Features.ChampionPurchases.Buy(client, itemUsage);
                return;
            }
            if (Revora.Game.Features.TravelScrollShop.TryBuy(client, itemUsage)) return;
            if (Revora.Game.Features.ExemptionTokenShop.TryBuy(client, itemUsage)) return;
            if (Revora.Game.Features.NewcomerIntroduction.TryBuy(client, itemUsage)) return;
            if (Revora.Game.Features.EuroJerseyShop.TryBuy(client, itemUsage)) return;
            // Keep progression-shop currency and NPC rules ahead of general shop dispatch.
            if (itemUsage != null && Revora.Game.Features.SubclassShop.IsShop(itemUsage.UID))
            {
                Revora.Game.Features.SubclassShop.Buy(client, itemUsage);
                return;
            }
            if (Revora.Game.Features.WeaponBookShop.TryBuy(client, itemUsage)) return;
            if (Revora.Game.Features.CpShopPurchases.TryBuy(client, itemUsage)) return;
            if (client == null)
            {
                return;
            }

            if (itemUsage == null)
            {
                return;
            }

            if (client.Trade.InTrade)
            {
                return;
            }

            if (itemUsage.UID == 2888)
            {

            }
            #region Honor shop
            if (itemUsage.UID == Database.HonorShop.UID)
            {
                Revora.Game.Features.HonorPurchases.Buy(client, itemUsage.dwParam);
                return;
            }
            #endregion

            #region Race point shop
            if (itemUsage.UID == Database.REVORAPointhop.UID)
            {
                if (Database.REVORAPointhop.Shop == null || Database.REVORAPointhop.Shop.Items == null)
                    return;
                Database.REVORAPointhop.RacePointItem item;
                if (Database.REVORAPointhop.Shop.Items.TryGetValue(itemUsage.dwParam, out item))
                {
                    if (client.Inventory.Count <= 39)
                    {
                        if (client.Entity.RevoraPoint >= item.Price)
                        {
                            ConquerItem newItem = new GamePackets.ConquerItem(true);
                            newItem.ID = itemUsage.dwParam;
                            Database.ConquerItemBaseInformation ibi;
                            if (Database.ConquerItemInformation.BaseInformations.TryGetValue(newItem.ID, out ibi))
                            {
                                if (ibi.Durability > 0)
                                {
                                    newItem.Durability = ibi.Durability;
                                    newItem.MaximDurability = ibi.Durability;
                                }
                            }
                            client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);

                            client.Entity.TrySpendRevoraPoints((uint)(item.Price));
                        }
                        else
                        {
                            client.Send(new Message("You do not have enough Champion Points", Color.Red, Message.TopLeft));
                        }
                    }
                    else
                    {
                        client.Send(new Message("You do not have enough space in your inventory", Color.Red, Message.TopLeft));
                    }
                }
                else
                {
                    client.Send(new Message("Can not find this item", Color.Red, Message.TopLeft));
                }

                return;
            }
            #endregion

            Interfaces.INpc npc = null;
            if (client.Map.Npcs.TryGetValue(itemUsage.UID, out npc) || itemUsage.UID == 2888)
            {
                ConquerItem _ExistingItem;
                Database.ConquerItemInformation iteminfo = new Database.ConquerItemInformation(itemUsage.dwParam, 0);
                if (client.Inventory.Count == 40 && !client.Inventory.Contains(iteminfo.BaseInformation.ID, iteminfo.BaseInformation.StackSize, out _ExistingItem))
                {
                    return;
                }

                if (itemUsage.UID != 2888)
                {
                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17)
                    {
                        return;
                    }
                }

                Database.ShopFile.Shop shop = new Database.ShopFile.Shop();
                if (!Database.ShopFile.Shops.TryGetValue(itemUsage.UID, out shop))
                {
                    if (!Database.EShopFile.Shops.TryGetValue(itemUsage.UID, out shop))
                    {
                        shop = null;
                    }
                }

                if (shop != null && shop.UID != 0)
                {
                    if (!shop.Items.Contains(itemUsage.dwParam))
                    {
                        Console.WriteLine("" + client.Entity.Name + " intent� comprar " + itemUsage.dwParam + " del shop: " + itemUsage.UID);
                        client.Send("Error! You cannot buy: " + itemUsage.dwParam + " from shop: " + itemUsage.UID + ". This has been reported.");
                        return;
                    }
                    uint Amount = itemUsage.dwExtraInfo > 0 ? itemUsage.dwExtraInfo : 1;
                    ConquerItem item = new ConquerItem(true);
                    switch (shop.MoneyType)
                    {
                        case Database.ShopFile.MoneyType.Gold:
                            {
                                if (iteminfo.BaseInformation.GoldWorth * Amount > client.Entity.Money)
                                {
                                    return;
                                }

                                if (client.Entity.Money - (iteminfo.BaseInformation.GoldWorth * Amount) > client.Entity.Money)
                                {
                                    return;
                                }

                                item.ID = itemUsage.dwParam;
                                item.Durability = item.MaximDurability = iteminfo.BaseInformation.Durability;
                                item.Color = (Game.Enums.Color)3;

                                uint NewAmount = itemUsage.dwExtraInfo > 0 ? itemUsage.dwExtraInfo : 1;
                                while (NewAmount > 0)
                                {
                                    if (iteminfo.BaseInformation.StackSize == 0)
                                    {
                                        item = new ConquerItem(true);
                                        item.ID = itemUsage.dwParam;
                                        item.Durability = item.MaximDurability = iteminfo.BaseInformation.Durability;
                                        item.Color = (Game.Enums.Color)3;
                                        item.MaxStackSize = item.StackSize = 1;
                                        client.Inventory.Add(item, Game.Enums.ItemUse.CreateAndAdd);
                                        NewAmount--;
                                    }
                                    else
                                    {
                                        if (client.Inventory.GetStackContainer(iteminfo.BaseInformation.ID, iteminfo.BaseInformation.StackSize, 1, out _ExistingItem))
                                        {
                                            _ExistingItem.StackSize++;
                                            Database.ConquerItemTable.UpdateStack(_ExistingItem);
                                            _ExistingItem.Mode = Game.Enums.ItemMode.Update;
                                            _ExistingItem.Send(client);
                                            NewAmount -= 1;
                                        }
                                        else
                                        {
                                            item = new ConquerItem(true);
                                            item.ID = itemUsage.dwParam;
                                            item.Durability = item.MaximDurability = iteminfo.BaseInformation.Durability;
                                            item.Color = (Game.Enums.Color)3;
                                            item.MaxStackSize = iteminfo.BaseInformation.StackSize;
                                            item.StackSize = 1;
                                            if (NewAmount >= item.MaxStackSize)
                                            {
                                                item.StackSize = item.MaxStackSize;
                                                NewAmount -= item.StackSize;
                                            }
                                            else
                                            {
                                                item.StackSize = (ushort)NewAmount;
                                                NewAmount = 0;
                                            }
                                            client.Inventory.Add(item, Game.Enums.ItemUse.CreateAndAdd);
                                            Database.ConquerItemTable.UpdateStack(item);
                                        }
                                    }
                                }
                                client.Entity.AdjustMoney(-(decimal)((iteminfo.BaseInformation.GoldWorth * Amount)));
                                break;
                            }
                        case Database.ShopFile.MoneyType.ConquerPoints:
                        case Database.ShopFile.MoneyType.BoundCps:
                            // All CP purchases must pass the transactional, binding-aware route above.
                            return;
                    }
                }
            }
        }
        public static void HandleSellToNPC(ItemUsage itemUsage, GameState client)
        {
            Interfaces.INpc npc = null;
            if (client.Map.Npcs.TryGetValue(itemUsage.UID, out npc))
            {
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 17)
                {
                    return;
                }

                ConquerItem item = null;
                if (client.Inventory.TryGetItem(itemUsage.dwParam, out item))
                {
                    if (Revora.Game.Features.MoonBoxQuest.Material(item.ID)) return;
                    if (item.Lock != 0 || item.Suspicious)
                    {
                        return;
                    }

                    uint Price = new Database.ConquerItemInformation(item.ID, 0).BaseInformation.GoldWorth;
                    Price = Price / 3;
                    if (item.Durability > 0 && item.Durability < item.MaximDurability)
                    {
                        Price = (Price * item.Durability) / item.MaximDurability;
                    }

                    if (item.Durability > 0 && item.Durability <= item.MaximDurability)
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                        client.Entity.AdjustMoney((decimal)(Price));
                    }
                    else
                    {
                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    }
                }
            }
        }
        public static void HandleVIPRepair(ItemUsage itemUsage, GameState client)
        {
            foreach (var item in client.Equipment.Objects)
            {
                if (item != null)
                {
                    if (item.Durability != 0 && item.Durability != item.MaximDurability)
                    {
                        if (item.Suspicious)
                        {
                            return;
                        }

                        if (IsArrow(item.ID))
                        {
                            return;
                        }

                        var info = new Database.ConquerItemInformation(item.ID, 0).BaseInformation;
                        if (item.Durability > 0 && item.Durability < info.Durability)
                        {
                            uint Price = info.GoldWorth;
                            byte Quality = (byte)(item.ID % 10);
                            double QualityMultipier = 0;

                            switch (Quality)
                            {
                                case 9: QualityMultipier = 1.125; break;
                                case 8: QualityMultipier = 0.975; break;
                                case 7: QualityMultipier = 0.9; break;
                                case 6: QualityMultipier = 0.825; break;
                                default: QualityMultipier = 0.75; break;
                            }

                            int nRepairCost = 0;
                            if (Price > 0)
                            {
                                nRepairCost = (int)Math.Ceiling((Price * (info.Durability - item.Durability) / info.Durability) * QualityMultipier);
                            }

                            nRepairCost = Math.Max(1, nRepairCost);
                            if (client.Entity.Money >= (ulong)nRepairCost)
                            {
                                client.Entity.AdjustMoney(-(decimal)((ulong)nRepairCost));
                                item.Durability = item.MaximDurability = info.Durability;
                                item.Mode = Game.Enums.ItemMode.Update;
                                item.Send(client);
                                Database.ConquerItemTable.UpdateDurabilityItem(item);
                            }
                            else
                            {
                                client.Send(new NpcReply(NpcReply.MessageBox, "You did not have enough money to fix your items!"));
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static void HandleRepair(ItemUsage itemUsage, GameState client)
        {
            if(itemUsage!=null)Revora.Game.Features.EquipmentUpgrades.Repair(client,itemUsage.UID);
        }
        public static void UpgradeItem(ItemUsage itemUsage, GameState client)
        {
            if(itemUsage==null || client==null || client.ItemSyncRoot==null)return;
            lock(client.ItemSyncRoot) {
                if(!Revora.Game.Features.QuestInventory.Player(client))return;
                ConquerItem material;
                if(!client.Inventory.TryGetItem(itemUsage.dwParam,out material))return;
                if(material.ID==1088000 || material.ID==1088001) {
                    Revora.Game.Features.EquipmentUpgrades.Apply(client,itemUsage.UID,new[]{material.UID},
                        material.ID==1088001?Revora.Game.Features.EquipmentUpgrades.Operation.MeteorLevel:Revora.Game.Features.EquipmentUpgrades.Operation.DragonQuality,
                        Revora.Game.Features.EquipmentUpgrades.Interface.Direct);
                    return;
                }
                // MeteorTear reduction retains its existing separate behavior.
            ConquerItem item = null;
            if (client.Inventory.TryGetItem(itemUsage.UID, out item))
            {
                if (IsArrow(item.ID))
                {
                    return;
                }

                ConquerItem upgrade = null;
                if (client.Inventory.TryGetItem(itemUsage.dwParam, out upgrade))
                {
                    Database.ConquerItemInformation infos = new Database.ConquerItemInformation(item.ID, item.Plus);
                    switch (upgrade.ID)
                    {
                        case 1088002:
                            {
                                if (infos.BaseInformation.Level <= 15)
                                {
                                    return;
                                }

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
                                item = ItemSocket(item, 2);
                                client.Inventory.Remove(upgrade, Game.Enums.ItemUse.Remove);
                                break;
                            }
                    }
                }
            }
            }
        }
        public static ConquerItem ItemSocket(ConquerItem item, byte type)
        {
            if (type == 1)
            {
                item.SocketProgress += 10;
            }
            else
            {
                item.SocketProgress += 5;
            }

            ushort need = 0;
            if (item.SocketOne == Game.Enums.Gem.NoSocket && item.SocketTwo == Game.Enums.Gem.NoSocket)
            {
                need = Constants.SocketOneProgress;
            }
            else if (item.SocketOne != Game.Enums.Gem.NoSocket && item.SocketTwo == Game.Enums.Gem.NoSocket)
            {
                need = Constants.SocketTwoProgress;
            }

            if (item.SocketProgress >= need && need != 0)
            {
                item.SocketProgress -= need;
                if (need == Constants.SocketOneProgress)
                {
                    item.SocketOne = Game.Enums.Gem.EmptySocket;
                }
                else
                {
                    item.SocketTwo = Game.Enums.Gem.EmptySocket;
                }

                Database.ConquerItemTable.UpdateSockets(item);
            }
            Database.ConquerItemTable.UpdateSocketProgress(item);
            return item;
        }
        public static byte ItemMaxLevel(ushort postion)
        {
            switch (postion)
            {
                case 0: return 0;
                case ConquerItem.Head: return 140;
                case ConquerItem.Necklace: return 139;
                case ConquerItem.Armor: return 140;
                case ConquerItem.LeftWeapon: return 140;
                case ConquerItem.RightWeapon: return 140;
                case ConquerItem.Boots: return 129;
                case ConquerItem.Ring: return 136;
                case ConquerItem.Tower: return 100;
                case ConquerItem.Fan: return 100;
                case ConquerItem.Steed: return 0;
                case ConquerItem.SteedCrop: return 30;
                case ConquerItem.Wing: return 100;
            }
            return 0;
        }
        public static byte ItemMinLevel(ushort postion)
        {
            switch (postion)
            {
                case 0: return 0;
                case ConquerItem.Head: return 15;
                case ConquerItem.Necklace: return 7;
                case ConquerItem.Armor: return 15;
                case ConquerItem.LeftWeapon: return 40;
                case ConquerItem.RightWeapon: return 5;
                case ConquerItem.Boots: return 10;
                case ConquerItem.Ring: return 10;
                case ConquerItem.Tower: return 0;
                case ConquerItem.Fan: return 0;
                case ConquerItem.Steed: return 0;
                case ConquerItem.SteedCrop: return 0;
                case ConquerItem.Wing: return 0;

            }
            return 0;
        }
        public static Positions GetPositionFromID(UInt32 itemid)
        {
            UInt32 iType = itemid / 1000;
            if (iType == 145)
            {
                return Positions.Head;
            }

            if (iType == 4100)
            {
                return Positions.Relics;
            }

            if (iType == 617)
            {
                return Positions.Right;
            }

            if (iType == 148)
            {
                return Positions.Head;
            }

            if (iType == 614)
            {
                return Positions.Right;
            }

            if (iType == 615 || iType == 616)
            {
                return Positions.Right;
            }

            if (iType >= 111 && iType <= 118 || iType == 123 || iType >= 141 && iType <= 148 || iType == 170)
            {
                return Positions.Head;
            }
            else if (iType >= 120 && iType <= 121)
            {
                return Positions.Necklace;
            }
            else if (iType == 622)
            {
                return Positions.Right;
            }
            else if (iType == 624)
            {
                return Positions.Right;
            }
            else if (iType == 626)
            {
                return Positions.Right;
            }
            else if (iType >= 130 && iType <= 139 || iType == 101)
            {
                return Positions.Armor;
            }
            else if (iType >= 150 && iType <= 152)
            {
                return Positions.Ring;
            }
            else if (iType == 160)
            {
                return Positions.Boots;
            }
            else if (iType >= 181 && iType <= 199)
            {
                return Positions.Garment;
            }
            else if (iType == 201)
            {
                return Positions.AttackTalisman;
            }
            else if (iType == 202)
            {
                return Positions.DefenceTalisman;
            }
            else if (iType == 203)
            {
                return Positions.SteedTalisman;
            }
            else if (iType == 204)
            {
                return Positions.Wing;
            }
            else if (iType == 200)
            {
                return Positions.SteedArmor;
            }
            else if (iType == 300)
            {
                return Positions.Steed;
            }
            else if (iType == 2100)
            {
                return Positions.Bottle;
            }
            else if (iType == 1050 || iType == 900 || iType == 619 /*|| iType == 670*/)
            {
                return Positions.Left;
            }
            else if ((iType >= 410 && iType <= 490) || (iType >= 500 && iType <= 580) || (iType >= 601 && iType <= 614) || (iType >= 616 && iType <= 617) || iType == 620 || iType == 671 || iType == 670)
            {
                return Positions.Right;
            }
            else if (iType >= 350 && iType <= 380)
            {
                return Positions.RightAccessory;
            }
            else if (iType == 380)
            {
                return Positions.LeftAccessory;
            }
            else
            {
                return (Positions)0;
            }
        }
        public static ushort ItemPosition(uint ID)
        {
            if (ID >= 4100001 && ID <= 4100005)
            {
                return ConquerItem.Relics;
            }

            if (ID >= 170000 && ID <= 170309)
            {
                return ConquerItem.Head;
            }

            if (ID >= 101000 && ID <= 101309)
            {
                return ConquerItem.Armor;
            }

            if (ID >= 626003 && ID <= 626439)
            {
                return ConquerItem.RightWeapon;
            }

            if ((ID >= 111003 && ID <= 118309) || (ID >= 123000 && ID <= 123309) || (ID >= 141003 && ID <= 145309) || (ID >= 148000 && ID <= 148309))
            {
                return ConquerItem.Head;
            }
            else if (ID >= 120001 && ID <= 121269)
            {
                return ConquerItem.Necklace;
            }
            else if (ID >= 130003 && ID <= 139309)
            {
                return ConquerItem.Armor;
            }
            else if (ID >= 150000 && ID <= 152279)
            {
                return ConquerItem.Ring;
            }
            else if (ID >= 160013 && ID <= 160249)
            {
                return ConquerItem.Boots;
            }
            else if (ID >= 181305 && ID <= 194300)
            {
                return ConquerItem.Garment;
            }
            else if (ID >= 201003 && ID <= 201009)
            {
                return ConquerItem.Fan;
            }
            else if (ID >= 202003 && ID <= 202009)
            {
                return ConquerItem.Tower;
            }
            else if (ID >= 203003 && ID <= 203009)
            {
                return ConquerItem.SteedCrop;
            }
            else if (ID == 300000)
            {
                return ConquerItem.Steed;
            }
            else if (ID >= 410003 && ID <= 617439 || ID >= 620003 && ID <= 620439 || ID >= 622000 && ID <= 622439 || ID >= 624000 && ID <= 624439 || ID >= 671000 && ID <= 671439)
            {
                return ConquerItem.RightWeapon;
            }
            else if ((ID >= 900000 && ID <= 900309) || ID >= 1050000 && ID <= 1051000 || ID >= 619000 && ID <= 619439 || ID >= 670000 && ID <= 670439)
            {
                return ConquerItem.LeftWeapon;
            }
            else if (ID >= 2100005 && ID <= 2100099)
            {
                return ConquerItem.Bottle;
            }
            else if (ID >= 350001 && ID <= 380030)
            {
                return ConquerItem.LeftWeaponAccessory;
            }
            else if (ID >= 350001 && ID <= 380030)
            {
                return ConquerItem.RightWeaponAccessory;
            }
            else if (ID >= 200000 && ID <= 200540)
            {
                return ConquerItem.SteedArmor;
            }
            else if (ID >= 204003 && ID <= 204009)
            {
                return ConquerItem.Wing;
            }

            return 0;
        }
        public static bool IsArrow(uint ID)
        {
            if (ID >= 1050000 && ID <= 1050002)
            {
                return true;
            }

            return false;
        }
        public static bool IsScythe(uint ID)
        {
            if (ID >= 511000 && ID <= 511439)
            { return true; }
            else { return false; }
        }
        public static bool IsWing(uint ID)
        {
            if (ID >= 204005 && ID <= 205129)
            {
                return true;
            }

            return false;
        }
        public static bool IsWarriorArmor(uint ID)
        {
            if (ID >= 131003 && ID <= 131309)
            { return true; }
            else { return false; }
        }
        public static bool IsHossu(uint ID)
        {
            if (ID >= 619000 && ID <= 619439)
            {
                return true;
            }

            return false;
        }
        public static bool IsWarriorHelmet(uint ID)
        {
            if (ID >= 111003 && ID <= 111309)
            { return true; }
            else { return false; }
        }
        public static bool IsKnife(uint ID)
        {
            if (ID >= 613000 && ID <= 613429)
            { return true; }
            else { return false; }
        }
        public static bool IsKatana(uint ID)
        {
            if (ID >= 601000 && ID <= 601439)
            { return true; }
            else { return false; }
        }
        public static bool IsCrossSaber(uint ID)
        {
            if (ID >= 614000 && ID <= 614439)
            { return true; }
            else { return false; }
        }
        public static bool IsRapier(uint ID)
        {
            if (ID >= 611000 && ID <= 611439)
            { return true; }
            else { return false; }
        }
        public static bool IsPistol(uint ID)
        {
            if (ID >= 612000 && ID <= 612439)
            { return true; }
            else { return false; }
        }
        public static bool IsBacksword(uint ID)
        {
            if (ID >= 421003 && ID <= 421338)
            {
                return true;
            }

            return false;
        }
        public static bool IsBeads(uint ID)
        {
            if (ID >= 610000 && ID <= 610439)
            {
                return true;
            }

            return false;
        }
        public static bool IsTwoHand(uint ID)
        {
            return (ID.ToString()[0] == '5' ? true : false);
        }
        public static bool IsAccessory(uint ID)
        {
            return ID >= 350001 && ID <= 380015;
        }
        public static bool WarriorEpic(uint p)
        {
            if (p >= 624000 && p <= 619439)
            {
                return true;
            }

            if (p >= 624439 && p <= 624439)
            {
                return true;
            }

            return false;
        }
        public static bool WindWalkerItem(uint p)
        {
            if (p >= 626000 && p <= 626439)
            {
                return true;
            }

            if (p >= 626439 && p <= 626439)
            {
                return true;
            }

            return false;
        }
        private static bool EpicasTaoistaItem(uint p)
        {
            if (p >= 619000 && p <= 619439)
            {
                return true;
            }

            if (p >= 620003 && p <= 620439)
            {
                return true;
            }

            return false;
        }
        private static bool PirateEpic(uint p)
        {
            if (p >= 670003 && p <= 670439)
            {
                return true;
            }

            if (p >= 67103 && p <= 671439)
            {
                return true;
            }

            return false;
        }
        public enum Positions : byte
        {
            Inventory = 0,
            Head = 1,
            Necklace = 2,
            Armor = 3,
            Right = 4,
            Left = 5,
            Ring = 6,
            Bottle = 7,
            Boots = 8,
            Garment = 9,
            AttackTalisman = 10,
            DefenceTalisman = 11,
            Steed = 12,
            Relics = 13,
            RightAccessory = 15,
            LeftAccessory = 16,
            SteedArmor = 17,
            SteedTalisman = 18,
            Wing = 19
        }
        public static Positions ItemPositionFromID(UInt32 itemid)
        {
            UInt32 iType = itemid / 1000;

            if (iType >= 111 && iType <= 118 || iType == 123 || iType >= 141 && iType <= 143 || iType == 145)
            {
                return Positions.Head;
            }
            else if (iType >= 120 && iType <= 121)
            {
                return Positions.Necklace;
            }
            else if (iType >= 130 && iType <= 137)
            {
                return Positions.Armor;
            }
            else if (iType >= 150 && iType <= 152)
            {
                return Positions.Ring;
            }
            else if (iType == 160)
            {
                return Positions.Boots;
            }
            else if (iType >= 181 && iType <= 199)
            {
                return Positions.Garment;
            }
            else if (iType >= 204 && iType <= 205)
            {
                return Positions.Wing;
            }
            else if (iType == 201)
            {
                return Positions.AttackTalisman;
            }
            else if (iType == 202)
            {
                return Positions.DefenceTalisman;
            }
            else if (iType == 203)
            {
                return Positions.SteedTalisman;
            }
            else if (iType == 200)
            {
                return Positions.SteedArmor;
            }
            else if (iType == 300)
            {
                return Positions.Steed;
            }
            else if (iType == 2100)
            {
                return Positions.Bottle;
            }
            else if (iType == 1050 || iType == 900 || (iType == 670))
            {
                return Positions.Left;
            }
            else if ((iType >= 410 && iType <= 490) || (iType >= 500 && iType <= 580) || (iType >= 601 && iType <= 613) || (iType >= 616 && iType <= 617 || (iType >= 619 && iType <= 620)) || (iType == 622) || (iType == 624) || (iType == 671))
            {
                return Positions.Right;
            }
            else if (iType >= 350 && iType <= 370)
            {
                return Positions.RightAccessory;
            }
            else if (iType == 380)
            {
                return Positions.LeftAccessory;
            }
            else
            {
                return (Positions)0;
            }
        }
        public static void EquipItem(ItemUsage itemUsage, GameState client)
        {
            ConquerItem item = null;
            client.Entity.AttackPacket = null;
            if (client.Inventory.TryGetItem(itemUsage.UID, out item) && client.Booth == null)
            {
                // Accessories may choose a hand, not an arbitrary equipment slot.
                if (IsAccessory(item.ID) && itemUsage.dwParam != ConquerItem.RightWeaponAccessory
                    && itemUsage.dwParam != ConquerItem.LeftWeaponAccessory)
                    return;
                if (item.Suspicious)
                {
                    return;
                }

                Positions pos = GetPositionFromID(item.ID);
                if (pos == Positions.Inventory)
                {
                    UseItem(item, client); return;
                }
                if (ItemPosition(item.ID) == ConquerItem.Garment)
                {
                    if (client.Entity.Tournament_Signed)
                    {
                        return;
                    }
                }
                #region Anticheat
                if (!IsLegalRevoraTalisman(item))
                {
                #region HP
                if (ItemPosition(item.ID) == ConquerItem.Wing ||
                    //ItemPosition(item.ID) == ConquerItem.Steed ||
                    ItemPosition(item.ID) == ConquerItem.Tower ||
                    ItemPosition(item.ID) == ConquerItem.Fan ||
                    ItemPosition(item.ID) == ConquerItem.Bottle ||
                    ItemPosition(item.ID) == ConquerItem.Garment ||
                    ItemPosition(item.ID) == ConquerItem.SteedArmor ||
                    ItemPosition(item.ID) == ConquerItem.SteedCrop ||
                    ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory ||
                    ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                {
                    if (item.Enchant > 0)
                    {

                        if (client.Entity.MapID != 6004)
                        {
                            #region Alas o Wing
                            if (ItemPosition(item.ID) == ConquerItem.Wing)
                            {
                                client.Equipment.Remove(19);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region Caballo o Steed
                            //if (ItemPosition(item.ID) == ConquerItem.Steed)
                            //{
                            //    client.Equipment.Remove(12);
                            //    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                            //    client.Inventory.Objects.CopyTo(inventory, 0);
                            //    client.MessageBox("Eng: You have been sent to bot jail for being suspicious of items enchanted. \nEsp: Has sido enviado a la carcel de bot por ser sospechoso de items HP.", null, null, 0);
                            //    client.Player.Teleport(6004, 30, 73);

                            //    Console.WriteLine("" + client.Player.Name + " fue apresado por utilizar un item bug con HP.");
                            //}
                            #endregion
                            #region Torro o Tower
                            if (ItemPosition(item.ID) == ConquerItem.Tower)
                            {
                                client.Equipment.Remove(11);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region Abanico o fan
                            if (ItemPosition(item.ID) == ConquerItem.Fan)
                            {
                                client.Equipment.Remove(10);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region Copa o Bottle
                            if (ItemPosition(item.ID) == ConquerItem.Bottle)
                            {
                                client.Equipment.Remove(7);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region Frack o Garment
                            if (ItemPosition(item.ID) == ConquerItem.Garment)
                            {
                                client.Equipment.Remove(9);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region FrackCaballo o SteedArmor
                            if (ItemPosition(item.ID) == ConquerItem.SteedArmor)
                            {
                                client.Equipment.Remove(17);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region Latigo o SteedCrop
                            if (ItemPosition(item.ID) == ConquerItem.SteedCrop)
                            {
                                client.Equipment.Remove(18);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region FRacDeArmaDerecho o LeftWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory)
                            {
                                client.Equipment.Remove(16);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion
                            #region FRacDeArmaIzq o RightWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                            {
                                client.Equipment.Remove(15);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious enchanted items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion

                        }
                        return;

                    }
                }
                #endregion
                #region Bless
                if (ItemPosition(item.ID) == ConquerItem.Wing ||
                    ItemPosition(item.ID) == ConquerItem.Tower ||
                    //ItemPosition(item.ID) == ConquerItem.Steed ||
                    ItemPosition(item.ID) == ConquerItem.Fan ||
                    ItemPosition(item.ID) == ConquerItem.Bottle ||
                    ItemPosition(item.ID) == ConquerItem.Garment ||
                    ItemPosition(item.ID) == ConquerItem.SteedArmor ||
                    ItemPosition(item.ID) == ConquerItem.SteedCrop ||
                    ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory ||
                    ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                {
                    if (item.Bless > 3)
                    {
                        if (client.Entity.MapID != 6004)
                        {
                            #region Alas o Wing
                            if (ItemPosition(item.ID) == ConquerItem.Wing)
                            {
                                client.Equipment.Remove(19);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Caballo o Steed
                            //if (ItemPosition(item.ID) == ConquerItem.Steed)
                            //{
                            //    client.Equipment.Remove(12);
                            //    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                            //    client.Inventory.Objects.CopyTo(inventory, 0);
                            //    client.MessageBox("Eng: You have been sent to bot jail for being suspicious of items enchanted. \nEsp: Has sido enviado a la carcel de bot por ser sospechoso de items da�o.", null, null, 0);
                            //    client.Player.Teleport(6004, 30, 73);

                            //    Console.WriteLine("" + client.Player.Name + " fue apresado por utilizar un item bug con HP.");
                            //}
                            #endregion
                            #region Torro o Tower
                            if (ItemPosition(item.ID) == ConquerItem.Tower)
                            {
                                client.Equipment.Remove(11);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Abanico o fan
                            if (ItemPosition(item.ID) == ConquerItem.Fan)
                            {
                                client.Equipment.Remove(10);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Copa o Bottle
                            if (ItemPosition(item.ID) == ConquerItem.Bottle)
                            {
                                client.Equipment.Remove(7);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Frack o Garment
                            if (ItemPosition(item.ID) == ConquerItem.Garment)
                            {
                                client.Equipment.Remove(9);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FrackCaballo o SteedArmor
                            if (ItemPosition(item.ID) == ConquerItem.SteedArmor)
                            {
                                client.Equipment.Remove(17);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Latigo o SteedCrop
                            if (ItemPosition(item.ID) == ConquerItem.SteedCrop)
                            {
                                client.Equipment.Remove(18);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaDerecho o LeftWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory)
                            {
                                client.Equipment.Remove(16);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaIzq o RightWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                            {
                                client.Equipment.Remove(15);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug");
                            }
                            #endregion
                        }
                        return;
                    }
                }
                #endregion
                #region Socket
                if (ItemPosition(item.ID) == ConquerItem.Steed ||
                    ItemPosition(item.ID) == ConquerItem.Bottle ||
                    ItemPosition(item.ID) == ConquerItem.Garment ||
                    ItemPosition(item.ID) == ConquerItem.SteedArmor ||
                    ItemPosition(item.ID) == ConquerItem.SteedCrop ||
                    ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory ||
                    ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                {
                    if (item.SocketOne > 0 || item.SocketTwo > 0)
                    {
                        if (client.Entity.MapID != 6004)
                        {

                            #region Caballo o Steed
                            if (ItemPosition(item.ID) == ConquerItem.Steed)
                            {
                                client.Equipment.Remove(12);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious socketed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug con HP.");
                            }
                            #endregion 
                            #region Copa o Bottle
                            if (ItemPosition(item.ID) == ConquerItem.Bottle)
                            {
                                client.Equipment.Remove(7);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Frack o Garment
                            if (ItemPosition(item.ID) == ConquerItem.Garment)
                            {
                                client.Equipment.Remove(9);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FrackCaballo o SteedArmor
                            if (ItemPosition(item.ID) == ConquerItem.SteedArmor)
                            {
                                client.Equipment.Remove(17);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Latigo o SteedCrop
                            if (ItemPosition(item.ID) == ConquerItem.SteedCrop)
                            {
                                client.Equipment.Remove(18);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaDerecho o LeftWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory)
                            {
                                client.Equipment.Remove(16);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaIzq o RightWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                            {
                                client.Equipment.Remove(15);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug");
                            }
                            #endregion
                        }
                        return;
                    }
                }
                #region Tower, Fan & wings
                if (ItemPosition(item.ID) == ConquerItem.Tower)

                {

                    if (item.SocketOne == Game.Enums.Gem.NormalDragonGem || item.SocketOne == Game.Enums.Gem.NormalFuryGem || item.SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        item.SocketOne == Game.Enums.Gem.NormalMoonGem || item.SocketOne == Game.Enums.Gem.NormalPhoenixGem || item.SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        item.SocketOne == Game.Enums.Gem.NormalThunderGem || item.SocketOne == Game.Enums.Gem.NormalTortoiseGem || item.SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedFuryGem || item.SocketOne == Game.Enums.Gem.RefinedKylinGem || item.SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedPhoenixGem || item.SocketOne == Game.Enums.Gem.RefinedRainbowGem || item.SocketOne == Game.Enums.Gem.RefinedThunderGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedTortoiseGem || item.SocketOne == Game.Enums.Gem.RefinedVioletGem || item.SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperFuryGem || item.SocketOne == Game.Enums.Gem.SuperKylinGem || item.SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperPhoenixGem || item.SocketOne == Game.Enums.Gem.SuperRainbowGem || item.SocketOne == Game.Enums.Gem.SuperThunderGem ||
                        item.SocketOne == Game.Enums.Gem.SuperTortoiseGem || item.SocketOne == Game.Enums.Gem.SuperVioletGem || item.SocketTwo == Game.Enums.Gem.NormalDragonGem ||

                        item.SocketTwo == Game.Enums.Gem.NormalFuryGem || item.SocketTwo == Game.Enums.Gem.NormalKylinGem || item.SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalPhoenixGem || item.SocketTwo == Game.Enums.Gem.NormalRainbowGem || item.SocketTwo == Game.Enums.Gem.NormalThunderGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalTortoiseGem || item.SocketTwo == Game.Enums.Gem.NormalVioletGem || item.SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedKylinGem || item.SocketTwo == Game.Enums.Gem.RefinedMoonGem || item.SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedRainbowGem || item.SocketTwo == Game.Enums.Gem.RefinedThunderGem || item.SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedVioletGem || item.SocketTwo == Game.Enums.Gem.SuperDragonGem || item.SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperKylinGem || item.SocketTwo == Game.Enums.Gem.SuperMoonGem || item.SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperRainbowGem || item.SocketTwo == Game.Enums.Gem.SuperThunderGem || item.SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperVioletGem)
                    {
                        if (client.Entity.MapID != 6004)
                        {

                            if (ItemPosition(item.ID) == ConquerItem.Tower)
                            {
                                client.Equipment.Remove(11);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }

                        }
                        return;
                    }
                }
                if (ItemPosition(item.ID) == ConquerItem.Fan)
                {
                    if (item.SocketOne == Game.Enums.Gem.NormalDragonGem || item.SocketOne == Game.Enums.Gem.NormalFuryGem || item.SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        item.SocketOne == Game.Enums.Gem.NormalMoonGem || item.SocketOne == Game.Enums.Gem.NormalPhoenixGem || item.SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        item.SocketOne == Game.Enums.Gem.NormalGloryGem || item.SocketOne == Game.Enums.Gem.NormalTortoiseGem || item.SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedFuryGem || item.SocketOne == Game.Enums.Gem.RefinedKylinGem || item.SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedPhoenixGem || item.SocketOne == Game.Enums.Gem.RefinedRainbowGem || item.SocketOne == Game.Enums.Gem.RefinedGloryGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedTortoiseGem || item.SocketOne == Game.Enums.Gem.RefinedVioletGem || item.SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperFuryGem || item.SocketOne == Game.Enums.Gem.SuperKylinGem || item.SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperPhoenixGem || item.SocketOne == Game.Enums.Gem.SuperRainbowGem || item.SocketOne == Game.Enums.Gem.SuperGloryGem ||
                        item.SocketOne == Game.Enums.Gem.SuperTortoiseGem || item.SocketOne == Game.Enums.Gem.SuperVioletGem || item.SocketTwo == Game.Enums.Gem.NormalDragonGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalFuryGem || item.SocketTwo == Game.Enums.Gem.NormalKylinGem || item.SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalPhoenixGem || item.SocketTwo == Game.Enums.Gem.NormalRainbowGem || item.SocketTwo == Game.Enums.Gem.NormalGloryGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalTortoiseGem || item.SocketTwo == Game.Enums.Gem.NormalVioletGem || item.SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedKylinGem || item.SocketTwo == Game.Enums.Gem.RefinedMoonGem || item.SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedRainbowGem || item.SocketTwo == Game.Enums.Gem.RefinedGloryGem || item.SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedVioletGem || item.SocketTwo == Game.Enums.Gem.SuperDragonGem || item.SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperKylinGem || item.SocketTwo == Game.Enums.Gem.SuperMoonGem || item.SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperRainbowGem || item.SocketTwo == Game.Enums.Gem.SuperGloryGem || item.SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperVioletGem)
                    {
                        if (client.Entity.MapID != 6004)
                        {
                            if (ItemPosition(item.ID) == ConquerItem.Fan)
                            {
                                client.Equipment.Remove(10);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                        }
                        return;
                    }
                }
                if (ItemPosition(item.ID) == ConquerItem.Wing)
                {
                    if (item.SocketOne == Game.Enums.Gem.SuperGloryGem || item.SocketOne == Game.Enums.Gem.RefinedGloryGem || item.SocketOne == Game.Enums.Gem.NormalGloryGem ||

                        item.SocketOne == Game.Enums.Gem.NormalDragonGem || item.SocketOne == Game.Enums.Gem.NormalFuryGem || item.SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        item.SocketOne == Game.Enums.Gem.NormalMoonGem || item.SocketOne == Game.Enums.Gem.NormalPhoenixGem || item.SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        item.SocketOne == Game.Enums.Gem.NormalTortoiseGem || item.SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedFuryGem || item.SocketOne == Game.Enums.Gem.RefinedKylinGem || item.SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedPhoenixGem || item.SocketOne == Game.Enums.Gem.RefinedRainbowGem ||
                        item.SocketOne == Game.Enums.Gem.RefinedTortoiseGem || item.SocketOne == Game.Enums.Gem.RefinedVioletGem || item.SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperFuryGem || item.SocketOne == Game.Enums.Gem.SuperKylinGem || item.SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        item.SocketOne == Game.Enums.Gem.SuperPhoenixGem || item.SocketOne == Game.Enums.Gem.SuperRainbowGem ||
                        item.SocketOne == Game.Enums.Gem.SuperTortoiseGem || item.SocketOne == Game.Enums.Gem.SuperVioletGem || item.SocketTwo == Game.Enums.Gem.NormalDragonGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalFuryGem || item.SocketTwo == Game.Enums.Gem.NormalKylinGem || item.SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalPhoenixGem || item.SocketTwo == Game.Enums.Gem.NormalRainbowGem ||
                        item.SocketTwo == Game.Enums.Gem.NormalTortoiseGem || item.SocketTwo == Game.Enums.Gem.NormalVioletGem || item.SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedKylinGem || item.SocketTwo == Game.Enums.Gem.RefinedMoonGem || item.SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedRainbowGem || item.SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.RefinedVioletGem || item.SocketTwo == Game.Enums.Gem.SuperDragonGem || item.SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperKylinGem || item.SocketTwo == Game.Enums.Gem.SuperMoonGem || item.SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperRainbowGem || item.SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        item.SocketTwo == Game.Enums.Gem.SuperThunderGem || item.SocketTwo == Game.Enums.Gem.RefinedThunderGem || item.SocketTwo == Game.Enums.Gem.NormalThunderGem ||

                        item.SocketTwo == Game.Enums.Gem.SuperVioletGem)

                    {
                        if (client.Entity.MapID != 6004)
                        {

                            if (ItemPosition(item.ID) == ConquerItem.Wing)
                            {
                                client.Equipment.Remove(19);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }

                        }
                        return;
                    }
                }
                #endregion
                #endregion
                }
                #region Plus +
                if (ItemPosition(item.ID) == ConquerItem.Bottle ||
                    ItemPosition(item.ID) == ConquerItem.Garment ||
                    ItemPosition(item.ID) == ConquerItem.SteedArmor ||
                    ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory ||
                    ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                {
                    if (item.Plus > 0)
                    {
                        if (client.Entity.MapID != 6004)
                        {
                            #region Copa o Bottle
                            if (ItemPosition(item.ID) == ConquerItem.Bottle)
                            {
                                client.Equipment.Remove(7);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Frack o Garment
                            if (ItemPosition(item.ID) == ConquerItem.Garment)
                            {
                                client.Equipment.Remove(9);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FrackCaballo o SteedArmor
                            if (ItemPosition(item.ID) == ConquerItem.SteedArmor)
                            {
                                client.Equipment.Remove(17);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaDerecho o LeftWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory)
                            {
                                client.Equipment.Remove(16);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaIzq o RightWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                            {
                                client.Equipment.Remove(15);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug");
                            }
                            #endregion
                        }
                        return;
                    }
                }
                #endregion
                #region Perfection
                if (ItemPosition(item.ID) == ConquerItem.Bottle ||
                    ItemPosition(item.ID) == ConquerItem.Garment ||
                    ItemPosition(item.ID) == ConquerItem.SteedArmor ||
                    ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory ||
                    ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                {
                    if (item.PerfectionLevel > 0)
                    {
                        if (client.Entity.MapID != 6004)
                        {
                            #region Copa o Bottle
                            if (ItemPosition(item.ID) == ConquerItem.Bottle)
                            {
                                client.Equipment.Remove(7);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region Frack o Garment
                            if (ItemPosition(item.ID) == ConquerItem.Garment)
                            {
                                client.Equipment.Remove(9);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FrackCaballo o SteedArmor
                            if (ItemPosition(item.ID) == ConquerItem.SteedArmor)
                            {
                                client.Equipment.Remove(17);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaDerecho o LeftWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.LeftWeaponAccessory)
                            {
                                client.Equipment.Remove(16);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug ");
                            }
                            #endregion
                            #region FRacDeArmaIzq o RightWeaponAccessory
                            if (ItemPosition(item.ID) == ConquerItem.RightWeaponAccessory)
                            {
                                client.Equipment.Remove(15);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You have been sent to jail for suspicious blessed items.", null, null, 0);
                                client.Entity.Teleport(6004, 30, 73);

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item bug");
                            }
                            #endregion
                        }
                        if (client.Entity.MapID != 6001)
                        {
                            #region SegundoEquipoCasco o AlternateHead
                            if (ItemPosition(item.ID) == ConquerItem.AlternateHead)
                            {
                                client.Equipment.Remove(21);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();

                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoCollar o AlternateNecklace
                            if (ItemPosition(item.ID) == ConquerItem.AlternateNecklace)
                            {
                                client.Equipment.Remove(22);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoArmadura o AlternateArmor
                            if (ItemPosition(item.ID) == ConquerItem.AlternateArmor)
                            {
                                client.Equipment.Remove(23);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoArma o AlternateRightWeapon
                            if (ItemPosition(item.ID) == ConquerItem.AlternateRightWeapon)
                            {
                                client.Equipment.Remove(24);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoArma o AlternateLeftWeapon
                            if (ItemPosition(item.ID) == ConquerItem.AlternateLeftWeapon)
                            {
                                client.Equipment.Remove(25);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoAnillo o AlternateRing
                            if (ItemPosition(item.ID) == ConquerItem.AlternateRing)
                            {
                                client.Equipment.Remove(26);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoBotella o AlternateBottle
                            if (ItemPosition(item.ID) == ConquerItem.AlternateBottle)
                            {
                                client.Equipment.Remove(27);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoBotas o AlternateBoots
                            if (ItemPosition(item.ID) == ConquerItem.AlternateBoots)
                            {
                                client.Equipment.Remove(28);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion
                            #region SegundoEquipoFrack o AlternateGarment
                            if (ItemPosition(item.ID) == ConquerItem.AlternateGarment)
                            {
                                client.Equipment.Remove(28);
                                ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                client.Inventory.Objects.CopyTo(inventory, 0);
                                client.MessageBox("You cannot use Perfection on the second equipment set.", null, null, 0);
                                //client.Player.Teleport(6001, 30, 73);
                                //client.Disconnect();
                                Console.WriteLine("" + client.Entity.Name + " fue apresado por utilizar un item Con Perfeccion en Equipo secundario");
                            }
                            #endregion

                        }
                        return;
                    }
                }
                #endregion
                #endregion
                #region Sanity checks

                bool can2hand = false;
                bool can2wpn = false;
                uint dwExtra = 0;
                #region Epic Pirata
                if (client.Entity.Class >= 70 && client.Entity.Class <= 75)
                {
                    if (PirateEpic(item.ID))
                    {
                        can2hand = true;
                    }

                    can2wpn = true;
                }
                #endregion
                #region WindWalker
                if (client.Entity.Class >= 160 && client.Entity.Class <= 165)
                {
                    if (WindWalkerItem(item.ID))
                    {
                        can2hand = true;
                    }

                    can2wpn = true;
                }
                #endregion
                #region Epico War
                if (client.Entity.Class >= 20 && client.Entity.Class <= 25)
                {
                    if (WarriorEpic(item.ID))
                    {
                        can2hand = true;
                    }

                    can2wpn = true;
                }
                #endregion


                if (itemUsage.dwParam > 20)
                {
                    dwExtra = 20;
                }

                if (client.Entity.Class >= 11 && client.Entity.Class <= 75)
                {
                    can2hand = true;
                }

                if (client.Entity.Class >= 11 && client.Entity.Class <= 15 || client.Entity.Class >= 51 && client.Entity.Class <= 55 || client.Entity.Class >= 61 && client.Entity.Class <= 65 || client.Entity.Class >= 71 && client.Entity.Class <= 75)
                {
                    can2wpn = true;
                }

                if (client.Entity.Class >= 71 && client.Entity.Class <= 75 || client.Entity.Class >= 41 && client.Entity.Class <= 45)
                {
                    can2wpn = true;
                }

                if (client.Entity.Class >= 50 && client.Entity.Class <= 55)
                {
                    can2hand = true;
                    can2wpn = true;
                }
                if (client.Entity.Class >= 160 && client.Entity.Class <= 165)
                {
                    can2hand = true;
                    can2wpn = true;
                }
                if (client.Entity.Class >= 130 && client.Entity.Class <= 135 || client.Entity.Class >= 140 && client.Entity.Class <= 145)
                {
                    can2hand = true;
                    can2wpn = true;
                }
                if (client.Entity.Class >= 80 && client.Entity.Class <= 85)
                {
                    can2hand = true;
                    can2wpn = true;
                }
                //if (client.Player.Class >= 100)
                //    can2wpn = false;
                if (!Equipable(item, client))
                {
                    return;
                }

                if (ItemPosition(item.ID) == 5)
                {
                    itemUsage.dwParam = 5 + dwExtra;
                    if (!can2hand && !can2wpn)
                    {
                        return;
                    }

                    if (client.Equipment.Free(4 + dwExtra) || (client.Equipment.TryGetItem(4 + dwExtra).ID / 1000 != 500 && IsArrow(item.ID)))
                    {
                        return;
                    }
                }
                if (ItemPosition(item.ID) == 4)
                {
                    if (itemUsage.dwParam == 5 + dwExtra)
                    {
                        if (!can2hand || !can2wpn)
                        {
                            return;
                        }
                    }
                }
                if (!((itemUsage.dwParam == 4 + dwExtra || itemUsage.dwParam == 5 + dwExtra) && (ItemPosition(item.ID) == 4 || ItemPosition(item.ID) == 5)))
                {
                    if (!IsAccessory(item.ID))
                    {
                        itemUsage.dwParam = ItemPosition(item.ID) + dwExtra;
                    }
                }
                #region Hossu
                if (client.Entity.Class >= 130 && client.Entity.Class <= 135 || client.Entity.Class >= 140 && client.Entity.Class <= 145)
                {
                    if ((itemUsage.dwParam - dwExtra) == 5)
                    {
                        if ((item.ID / 1000) == 421 || (item.ID / 1000) == 620)
                        {
                            return;
                        }

                        var rItem = client.Equipment.TryGetItem((byte)(4 + dwExtra));
                        if (rItem != null)
                        {
                            if (item.ID / 1000 == 619)
                            {
                                if ((rItem.ID / 1000) != 421 && (rItem.ID / 1000) != 620)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                    if ((itemUsage.dwParam - dwExtra) == 4)
                    {
                        var rItem = client.Equipment.TryGetItem((byte)(5 + dwExtra));
                        if (rItem != null)
                        {
                            if (rItem.ID / 1000 == 619)
                            {
                                if ((item.ID / 1000) != 421 && (item.ID / 1000) != 620)
                                {
                                    return;
                                }
                            }

                        }
                    }
                }
                #endregion Hossu
                bool twohand = IsTwoHand(item.ID);
                if (!twohand && itemUsage.dwParam == 4 + dwExtra)
                {
                    if (!client.Equipment.Free((byte)(5 + dwExtra)))
                    {
                        if (client.Inventory.Count < 40)
                        {
                            if (IsArrow(client.Equipment.TryGetItem((byte)(5 + dwExtra)).ID))
                            {
                                client.Equipment.Remove((byte)(5 + dwExtra));
                            }
                            else
                            {
                                if (client.Equipment.TryGetItem((byte)(4 + dwExtra)) != null)
                                {
                                    if (IsTwoHand(client.Equipment.TryGetItem((byte)(4 + dwExtra)).ID))
                                    {
                                        client.Equipment.Remove((byte)(4 + dwExtra));
                                    }
                                }
                            }
                        }
                    }
                }

                if ((itemUsage.dwParam - dwExtra) == 5)
                {
                    if ((item.ID / 1000) == 421 || IsTwoHand(item.ID))
                    {
                        return;
                    }

                    var rItem = client.Equipment.TryGetItem((byte)(4 + dwExtra));
                    if (rItem != null)
                    {
                        if (rItem.IsTwoHander())
                        {
                            if (IsArrow(item.ID))
                            {
                                if (rItem.ID / 1000 != 500)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                if (IsShield(item.ID))
                                {
                                    if ((rItem.ID / 1000) == 421)
                                    {
                                        return;
                                    }

                                    if (!client.Spells.ContainsKey(10311) && client.Entity.FirstRebornClass == 25 && client.Entity.SecondRebornClass == 25)//Perseverance
                                    {
                                        client.Send(new Message("You need to know Perseverance (Warrior Pure skill) to be able to wear 2-handed weapon and shield.", System.Drawing.Color.Red, Message.Talk));
                                        return;
                                    }
                                }
                                else if (item.ID / 1000 == 619)
                                {
                                    if ((rItem.ID / 1000) != 421 && (rItem.ID / 1000) != 620)
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    itemUsage.dwParam = 4 + dwExtra;
                                }
                            }
                        }
                    }
                }
                if (itemUsage.dwParam == 4 + dwExtra)
                {
                    if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))
                    {
                        if (item.ID / 1000 != 500)
                        {
                            return;
                        }
                    }
                }

                if (!((client.Entity.Class >= 50 && client.Entity.Class <= 55)))
                {
                    if ((item.ID / 1000) == 601)
                    {
                        if ((itemUsage.dwParam - dwExtra) == 5)
                        {
                            return;
                        }
                    }
                }
                #endregion


                if (client.Map.ID == 1039)
                {
                    client.Entity.AttackPacket = null;
                }

                item.Position = (byte)itemUsage.dwParam;
                if (ItemPosition(item.ID) == 5 && !IsArrow(item.ID))
                {
                    if (IsTwoHand(client.Equipment.TryGetItem((byte)(4 + dwExtra)).ID))
                    {
                        if (!client.Spells.ContainsKey(10311))
                        {
                            client.Send(new Message("You need to know Perseverance (Warrior Pure skill) to be able to wear 2-handed weapon and shield.", System.Drawing.Color.Red, Message.Talk));
                            return;
                        }
                    }
                }
                #region Checks itemType
                uint itemType = item.ID / 1000;
                if (itemType == 421)//Backsword
                {
                    if (item.Position == 5 + dwExtra)
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 900)
                {
                    if (!(client.Entity.Class >= 20 && client.Entity.Class <= 25) &&
                        !(client.Entity.Class >= 60 && client.Entity.Class <= 65 && client.Entity.Reborn == 2))

                    {
                        item.Position = 0; return;
                    }

                }
                if (itemType == 613)//Knife
                {
                    if ((client.Entity.Class >= 70 && client.Entity.Class <= 75) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 50 && client.Entity.Class <= 55 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 511)//Scythe
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 601)//Katana
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 614)//CrossSaber
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 611)//Rapier
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 50 && client.Entity.Class <= 55 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 612)//Pistol
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 50 && client.Entity.Class <= 55 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 421)//Backsword
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 50 && client.Entity.Class <= 55 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 70 && client.Entity.Class <= 75))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 610) // Beads
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 50 && client.Entity.Class <= 55 || (client.Entity.Class >= 20 && client.Entity.Class <= 25 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135)))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 131 && !EquipPassRbReq(Database.ConquerItemInformation.BaseInformations[item.ID], client))//warrior armor
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135))))))
                    {
                        item.Position = 0; return;
                    }
                }
                if (itemType == 111 && !EquipPassRbReq(Database.ConquerItemInformation.BaseInformations[item.ID], client))//WarriorHelmet
                {
                    if ((client.Entity.Class >= 40 && client.Entity.Class <= 45) || (client.Entity.Class >= 70 && client.Entity.Class <= 75 || (client.Entity.Class >= 10 && client.Entity.Class <= 15 || (client.Entity.Class >= 60 && client.Entity.Class <= 65 || (client.Entity.Class >= 142 && client.Entity.Class <= 145 || (client.Entity.Class >= 132 && client.Entity.Class <= 135))))))
                    {
                        item.Position = 0; return;
                    }
                }
                #endregion
                client.Inventory.Remove(item, Game.Enums.ItemUse.Move, true);
                if (client.Equipment.Free((byte)itemUsage.dwParam))
                {
                    if (twohand)
                    {
                        client.Equipment.Remove((byte)(5 + dwExtra));
                    }

                    item.Position = (byte)itemUsage.dwParam;
                    client.Equipment.Add(item);
                    item.Mode = Game.Enums.ItemMode.Update;
                    item.Send(client);
                }
                else
                {
                    if (twohand)
                    {
                        client.Equipment.Remove((byte)(5 + dwExtra));
                    }

                    client.Equipment.Remove((byte)itemUsage.dwParam);
                    item.Position = (byte)itemUsage.dwParam;
                    client.Equipment.Add(item);
                }
                client.CalculateStatBonus();
                client.CalculateHPBonus();
                client.LoadItemStats();
                ClientEquip equips = new ClientEquip();
                equips.DoEquips(client);
                client.Send(equips);
                Database.ConquerItemTable.UpdateLocation(item, client);
                client.Equipment.UpdateEntityPacket();
                WindowStats Stats = new WindowStats(client);
                client.Send(Stats.ToArray());
                if (client.Team != null)
                {
                    client.Team.GetClanShareBp(client);
                }
            }
        }
        public static void UnequipItem(ItemUsage usage, GameState client)
        {
            if (client.LoginAt != DateTime.MinValue && DateTime.Now < client.LoginAt.AddSeconds(8))
                return;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow))
            {
                if (usage.dwParam == 4)
                {
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PathOfShadow);
                }
            }

            if (!client.Equipment.Free((byte)usage.dwParam))
            {
                if (ItemPosition(client.Equipment.TryGetItem((byte)usage.dwParam).ID) == ConquerItem.Garment)
                {
                    if (client.Entity.Tournament_Signed)
                    {
                        return;
                    }
                }
            }

            if (client.Equipment.Remove((byte)usage.dwParam))
            {
                if (client.Map.ID == 1039)
                {
                    client.Entity.AttackPacket = null;
                }

                client.CalculateStatBonus();
                client.CalculateHPBonus();
                client.LoadItemStats();
                ClientEquip equips = new ClientEquip();
                equips.DoEquips(client);
                client.Send(equips);
            }
            WindowStats Stats = new WindowStats(client);
            client.Send(Stats.ToArray());
            if (client.Team != null)
            {
                client.Team.GetClanShareBp(client);
            }
        }
        public static bool NulledClient(GameState client)
        {
            if (client == null)
            {
                return true;
            }

            if (client != null && client.Entity == null)
            {
                return true;
            }

            return false;
        }
        internal static bool IsShield(uint p)
        {
            return p / 1000 == 900;
        }
        internal static bool IsBow(uint p)
        {
            return p / 1000 == 500;
        }

        public static bool EquipPassLvlReq(Database.ConquerItemBaseInformation baseInformation, GameState client)
        {
            if (client.Entity.Level < baseInformation.Level)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public static bool EquipPassRbReq(Database.ConquerItemBaseInformation baseInformation, GameState client)
        {
            if (baseInformation.Level < 71 && client.Entity.Reborn > 0 && client.Entity.Level >= 70)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool EquipPassStatsReq(Database.ConquerItemBaseInformation baseInformation, GameState client)
        {
            if (client.Entity.Strength >= baseInformation.Strength && client.Entity.Agility >= baseInformation.Agility)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool EquipPassJobReq(Database.ConquerItemBaseInformation baseInformation, GameState client)
        {
            switch (baseInformation.Class)
            {
                #region Trojan
                case 10: if (client.Entity.Class <= 15 && client.Entity.Class >= 10) { return true; } break;
                case 11: if (client.Entity.Class <= 15 && client.Entity.Class >= 11) { return true; } break;
                case 12: if (client.Entity.Class <= 15 && client.Entity.Class >= 12) { return true; } break;
                case 13: if (client.Entity.Class <= 15 && client.Entity.Class >= 13) { return true; } break;
                case 14: if (client.Entity.Class <= 15 && client.Entity.Class >= 14) { return true; } break;
                case 15: if (client.Entity.Class == 15) { return true; } break;
                #endregion
                #region Warrior
                case 20: if (client.Entity.Class <= 25 && client.Entity.Class >= 20) { return true; } break;
                case 21: if (client.Entity.Class <= 25 && client.Entity.Class >= 21) { return true; } break;
                case 22: if (client.Entity.Class <= 25 && client.Entity.Class >= 22) { return true; } break;
                case 23: if (client.Entity.Class <= 25 && client.Entity.Class >= 23) { return true; } break;
                case 24: if (client.Entity.Class <= 25 && client.Entity.Class >= 24) { return true; } break;
                case 25: if (client.Entity.Class == 25) { return true; } break;
                #endregion
                #region Archer
                case 40: if (client.Entity.Class <= 45 && client.Entity.Class >= 40) { return true; } break;
                case 41: if (client.Entity.Class <= 45 && client.Entity.Class >= 41) { return true; } break;
                case 42: if (client.Entity.Class <= 45 && client.Entity.Class >= 42) { return true; } break;
                case 43: if (client.Entity.Class <= 45 && client.Entity.Class >= 43) { return true; } break;
                case 44: if (client.Entity.Class <= 45 && client.Entity.Class >= 44) { return true; } break;
                case 45: if (client.Entity.Class == 45) { return true; } break;
                #endregion
                #region Ninja
                case 50: if (client.Entity.Class <= 55 && client.Entity.Class >= 50) { return true; } break;
                case 51: if (client.Entity.Class <= 55 && client.Entity.Class >= 51) { return true; } break;
                case 52: if (client.Entity.Class <= 55 && client.Entity.Class >= 52) { return true; } break;
                case 53: if (client.Entity.Class <= 55 && client.Entity.Class >= 53) { return true; } break;
                case 54: if (client.Entity.Class <= 55 && client.Entity.Class >= 54) { return true; } break;
                case 55: if (client.Entity.Class == 55) { return true; } break;
                #endregion
                #region Monk
                case 60: if (client.Entity.Class <= 65 && client.Entity.Class >= 60) { return true; } break;
                case 61: if (client.Entity.Class <= 65 && client.Entity.Class >= 61) { return true; } break;
                case 62: if (client.Entity.Class <= 65 && client.Entity.Class >= 62) { return true; } break;
                case 63: if (client.Entity.Class <= 65 && client.Entity.Class >= 63) { return true; } break;
                case 64: if (client.Entity.Class <= 65 && client.Entity.Class >= 64) { return true; } break;
                case 65: if (client.Entity.Class == 65) { return true; } break;
                #endregion
                #region Pirate
                case 70: if (client.Entity.Class <= 75 && client.Entity.Class >= 70) { return true; } break;
                case 71: if (client.Entity.Class <= 75 && client.Entity.Class >= 71) { return true; } break;
                case 72: if (client.Entity.Class <= 75 && client.Entity.Class >= 72) { return true; } break;
                case 73: if (client.Entity.Class <= 75 && client.Entity.Class >= 73) { return true; } break;
                case 74: if (client.Entity.Class <= 75 && client.Entity.Class >= 74) { return true; } break;
                case 75: if (client.Entity.Class == 75) { return true; } break;
                #endregion
                #region Taoist
                case 190: if (client.Entity.Class >= 100) { return true; } break;
                #endregion
                #region LeeLong
                case 80: if (client.Entity.Class <= 85 && client.Entity.Class >= 80) { return true; } break;
                case 81: if (client.Entity.Class <= 85 && client.Entity.Class >= 81) { return true; } break;
                case 82: if (client.Entity.Class <= 85 && client.Entity.Class >= 82) { return true; } break;
                case 83: if (client.Entity.Class <= 85 && client.Entity.Class >= 83) { return true; } break;
                case 84: if (client.Entity.Class <= 85 && client.Entity.Class >= 84) { return true; } break;
                case 85: if (client.Entity.Class == 85) { return true; } break;
                #endregion
                #region Windwalker
                case 160: if (client.Entity.Class <= 165 && client.Entity.Class >= 160) { return true; } break;
                case 161: if (client.Entity.Class <= 165 && client.Entity.Class >= 161) { return true; } break;
                case 162: if (client.Entity.Class <= 165 && client.Entity.Class >= 162) { return true; } break;
                case 163: if (client.Entity.Class <= 165 && client.Entity.Class >= 163) { return true; } break;
                case 164: if (client.Entity.Class <= 165 && client.Entity.Class >= 164) { return true; } break;
                case 165: if (client.Entity.Class == 165) { return true; } break;
                #endregion
                case 0: return true;
                default: return false;
            }
            return false;
        }
        public static bool EquipPassSexReq(Database.ConquerItemBaseInformation baseInformation, GameState client)
        {
            int ClientGender = client.Entity.Body / 1000;
            if (baseInformation.Gender == ClientGender || baseInformation.Gender == 0)
            {
                return true;
            }

            return false;
        }
        public static bool Equipable(ConquerItem item, GameState client)
        {
            Database.ConquerItemBaseInformation BaseInformation = new Database.ConquerItemInformation(item.ID, item.Plus).BaseInformation;
            bool pass = false;
            if (!EquipPassSexReq(BaseInformation, client))
            {
                return false;
            }

            if (EquipPassRbReq(BaseInformation, client))
            {
                pass = true;
            }
            else
                if (EquipPassJobReq(BaseInformation, client))
            {
                if (EquipPassStatsReq(BaseInformation, client))
                {
                    if (EquipPassLvlReq(BaseInformation, client))
                    {
                        pass = true;
                    }
                }
            }

            if (!pass)
            {
                return false;
            }

            if (client.Entity.Reborn > 0)
            {
                if (client.Entity.Level >= 70 && BaseInformation.Level <= 70)
                {
                    return pass;
                }
                else
                {
                    Interfaces.IProf proficiency = null;
                    client.Proficiencies.TryGetValue((ushort)(item.ID / 1000), out proficiency);
                    pass = ItemPosition(item.ID) == ConquerItem.SteedArmor || BaseInformation.Proficiency == 0
                        || (proficiency != null && proficiency.Level >= BaseInformation.Proficiency);
                }
            }
            else
            {
                if (!IsArrow(item.ID))
                {
                    Interfaces.IProf proficiency = null;
                    client.Proficiencies.TryGetValue((ushort)(item.ID / 1000), out proficiency);
                    pass = ItemPosition(item.ID) == ConquerItem.SteedArmor || BaseInformation.Proficiency == 0
                        || (proficiency != null && proficiency.Level >= BaseInformation.Proficiency);
                }
            }
            return pass;
        }
    }
}
