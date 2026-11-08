using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using COServer.Systems_OverPower.WardRobeSystem;

namespace COServer.Network.GamePackets
{
    public sealed class CoatStorage
    {
        public CoatStorage() { }
        public bool Read(byte[] packet)
        {
            using (var memoryStream = new MemoryStream(packet))
            {
                Info = Serializer.DeserializeWithLengthPrefix<CoatStorageProto>(memoryStream, PrefixStyle.Fixed32);
            }
            return true;
        }
        public void Handle(Client.GameState client)
        {
            switch (Info.ActionId)
            {
              /*  case Action.Equipcheck:

                    if (Illegal(Info, client))
                        return;

                    ConquerItem myItem;
                    if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem))
                    {
                        var packet = FinalizeProtoBuf(new CoatStorageProto()
                        {
                            ActionId = Info.ActionId,
                            ItemGuid = Info.ItemGuid,
                        });
                        client.Send(packet);
                    }
                    break;*/
                case Action.Combine:
                    {
                        ConquerItem ItemInInventory;
                        ConquerItem ItemInSystem;
                        if (client.Player.StorageItems.TryGetValue(Info.ItemGuid, out ItemInSystem))
                        {
                            if (client.Inventory.TryGetItem(Info.ItemGuid, out ItemInInventory))
                            {
                                client.Inventory.Remove(ItemInInventory.UID, Game.Enums.ItemUse.Remove, true);
                            }
                        }
                        break;
                    }
                case Action.Equipcheck:

                    if (Illegal(Info, client))
                        return;

                    ConquerItem myItem;
                    if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem))
                    {
                        var packet = FinalizeProtoBuf(new CoatStorageProto()
                        {
                            ActionId = Info.ActionId,
                            ItemGuid = Info.ItemGuid,
                        });
                        client.Send(packet);
                    }
                    break;

                case Action.Addtowardrobe:
                    {
                        if (Illegal(Info, client))
                            return;

                        if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
                            client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                        {
                            if (!client.Player.StorageItems.ContainsKey(myItem.UID))
                            {
                                client.Player.StorageItems.Add(myItem.UID, myItem);
                                myItem.InWardrobe = true;
                                Database.ConquerItemTable.UpdateWardrobe(myItem.InWardrobe, myItem.UID);
                                client.Inventory.Remove(myItem, Game.Enums.ItemUse.None);
                            }
                            var dbInfo = Database.ConquerItemInformation.BaseInformations[myItem.ID];
                            var pos = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                            if (pos == 17)
                            {
                                  if (client.Player.SubClasses.Classes.ContainsKey(9))
                                    if (client.Player.SubClasses.Classes[9].Phase >= dbInfo.Proficiency)
                                    {
                                        client.Send(FinalizeProtoBuf(new CoatStorageProto()
                                        {
                                            ActionId = Info.ActionId,
                                            ItemGuid = Info.ItemGuid,
                                            ItemId = (int)myItem.ID
                                        }));
                                        foreach (var i in client.Player.StorageItems.Values)
                                            if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                                            {
                                                i.Position = 0;
                                                Database.ConquerItemTable.UpdateLocation(i, client);
                                            }
                                        myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);

                                        if (!client.Equipment.Add(myItem))
                                        {
                                            client.Equipment.Remove((byte)myItem.Position, true);
                                            if (!client.Equipment.Add(myItem))
                                                client.MessageBox("Sorry !");
                                            #region Combine Garment
                                            ConquerItem ItemInInventory;
                                            ConquerItem ItemInSystem;
                                            if (client.Player.StorageItems.TryGetValue(Info.ItemGuid, out ItemInSystem))
                                            {
                                                if (client.Inventory.TryGetItem(Info.ItemGuid, out ItemInInventory))
                                                {
                                                    client.Inventory.Remove(ItemInInventory.UID, Game.Enums.ItemUse.Remove, true);
                                                }
                                            }
                                            break;
                                            #endregion
                                        }
                                        var iu = new ItemUsage(true);
                                        iu.ID = (uint)ItemUsage.Mode.Unknown5;
                                        iu.UID = myItem.UID;
                                        iu.dwParam = myItem.Position;
                                        client.Send(iu.ToArray());

                                        ClientEquip equips = new ClientEquip();
                                        equips.DoEquips(client);
                                        client.Send(equips.ToArray());

                                        Database.ConquerItemTable.UpdateLocation(myItem, client);
                                        client.Equipment.UpdateEntityPacket();
                                    }
                                    else
                                        break;
                                if (Database.StorageItem.AmountStarMounts(client, 5) >= 1)
                                {
                                    new TitleStorage().AddTitle(client, 6004, 23, false);
                                }
                                if (Database.StorageItem.AmountStarMounts(client, 4) >= 5)
                                {
                                    new TitleStorage().AddTitle(client, 2006, 7, false);
                                }
                            }
                            if (pos == 9)
                            {
                                client.Send(FinalizeProtoBuf(new CoatStorageProto()
                                {
                                    ActionId = Info.ActionId,
                                    ItemGuid = Info.ItemGuid,
                                    ItemId = (int)myItem.ID
                                }));
                                foreach (var i in client.Player.StorageItems.Values)
                                    if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                                    {
                                        i.Position = 0;
                                        Database.ConquerItemTable.UpdateLocation(i, client);
                                    }
                                myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);

                                if (!client.Equipment.Add(myItem))
                                {
                                    client.Equipment.Remove((byte)myItem.Position, true);
                                    if (!client.Equipment.Add(myItem))
                                        client.MessageBox("Sorry !");
                                    #region GarmentCombination
                                    ConquerItem ItemInInventory;
                                    ConquerItem ItemInSystem;
                                    if (client.Player.StorageItems.TryGetValue(Info.ItemGuid, out ItemInSystem))
                                    {
                                        if (client.Inventory.TryGetItem(Info.ItemGuid, out ItemInInventory))
                                        {
                                            client.Inventory.Remove(ItemInInventory.UID, Game.Enums.ItemUse.Remove, true);
                                        }
                                    }
                                    break;
                                    #endregion
                                }



                                var iu = new ItemUsage(true);
                                iu.ID = (uint)ItemUsage.Mode.Unknown5;
                                iu.UID = myItem.UID;
                                iu.dwParam = myItem.Position;
                                client.Send(iu.ToArray());

                                ClientEquip equips = new ClientEquip();
                                equips.DoEquips(client);

                                if ((equips.Garment == 0 || equips.Garment > 0 && equips.Garment != myItem.UID) && myItem.Position != 17)
                                    equips.Garment = myItem.UID;

                                client.Send(equips.ToArray());

                                Database.ConquerItemTable.UpdateLocation(myItem, client);
                                client.Equipment.UpdateEntityPacket();
                                if (Database.StorageItem.AmountStarGarments(client, 5) >= 1)
                                {
                                    new TitleStorage().AddTitle(client, 6003, 22, false);
                                }
                                if (Database.StorageItem.AmountStarGarments(client, 4) >= 5)
                                {
                                    new TitleStorage().AddTitle(client, 2005, 6, false);
                                }
                            }
                            break;
                        }
                        if (Database.StorageItem.AmountStarGarments(client, 5) >= 1)
                        {
                            new TitleStorage().AddTitle(client, 6003, 22, false);
                        }
                        if (Database.StorageItem.AmountStarMounts(client, 5) >= 1)
                        {
                            new TitleStorage().AddTitle(client, 6004, 23, false);
                        }
                        if (Database.StorageItem.AmountStarGarments(client, 4) >= 5)
                        {
                            new TitleStorage().AddTitle(client, 2005, 6, false);
                        }
                        if (Database.StorageItem.AmountStarMounts(client, 4) >= 5)
                        {
                            new TitleStorage().AddTitle(client, 2006, 7, false);
                        }
                        break;
                    }
                #region Viejo AddToWardRobe(NO BORRAR)
           /*     case Action.Addtowardrobe:
                    {
                        if (Illegal(Info, client))
                            return;
                        if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
                            client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                        {
                            if (!client.Player.StorageItems.ContainsKey(myItem.UID))
                            {
                                client.Player.StorageItems.Add(myItem.UID, myItem);
                                myItem.InWardrobe = true;
                                Database.ConquerItemTable.UpdateWardrobe(myItem.InWardrobe, myItem.UID);
                                client.Inventory.Remove(myItem, Game.Enums.ItemUse.None);
                            }
                            var garments = new uint[] { 193445, 193525, 188915, 189065 }.ToList();
                            var mounts = new uint[] { 200449, 200517, 200531, 200540, 200482, 200482 }.ToList();
                            if (garments.Contains(myItem.ID))
                            {
                                new TitleStorage().AddTitle(client, 2005, 6, false);
                                new TitleStorage().AddTitle(client, 6003, 22, false);
                            }
                            else if (mounts.Contains(myItem.ID))
                            {
                                new TitleStorage().AddTitle(client, 2006, 7, false);
                                new TitleStorage().AddTitle(client, 6004, 23, false);
                            }
                            var dbInfo = Database.ConquerItemInformation.BaseInformations[myItem.ID];
                            var pos = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                            if (pos == 17)
                            {
                                if (client.Player.SubClasses.Classes.ContainsKey(9))
                                    if (client.Player.SubClasses.Classes[9].Phase >= dbInfo.Proficiency)
                                    {
                                        client.Send(FinalizeProtoBuf(new CoatStorageProto()
                                        {
                                            ActionId = Info.ActionId,
                                            ItemGuid = Info.ItemGuid,
                                            ItemId = (int)myItem.ID
                                        }));
                                        foreach (var i in client.Player.StorageItems.Values)
                                            if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                                            {
                                                i.Position = 0;
                                                Database.ConquerItemTable.UpdateLocation(i, client);
                                            }
                                        myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);

                                        if (!client.Equipment.Add(myItem))
                                        {
                                            client.Equipment.Remove((byte)myItem.Position, true);
                                            if (!client.Equipment.Add(myItem))
                                                throw new Exception();
                                        }
                                        var iu = new ItemUsage(true);
                                        iu.Action = ItemUsage.Mode.Unknown5;
                                        iu.UID = myItem.UID;
                                        iu.dwParam = myItem.Position;
                                        client.Send(iu.ToArray());
                                        ClientEquip equips = new ClientEquip();
                                        equips.DoEquips(client);
                                        client.Send(equips.ToArray());
                                        Database.ConquerItemTable.UpdateLocation(myItem, client);
                                        client.Equipment.UpdateEntityPacket();
                                    }
                                    else
                                        break;
                            }
                            if (pos == 9)
                            {
                                client.Send(FinalizeProtoBuf(new CoatStorageProto()
                                {
                                    ActionId = Info.ActionId,
                                    ItemGuid = Info.ItemGuid,
                                    ItemId = (int)myItem.ID
                                }));
                                foreach (var i in client.Player.StorageItems.Values)
                                    if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                                    {
                                        i.Position = 0;
                                        Database.ConquerItemTable.UpdateLocation(i, client);
                                    }
                                myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                                if (!client.Equipment.Add(myItem))
                                {
                                    client.Equipment.Remove((byte)myItem.Position, true);
                                    if (!client.Equipment.Add(myItem))
                                    {
                                    var CombineGarments = new uint[] { 192895, 185375, 187405, 187415, 187425, 188345, 188665, 187515, 188225, 187675, 183465, 185395, 184405, 188355, 185525, 182305, 182405, 182505, 182605, 182705, 182805, 182905, 186305, 188055, 184375, 184395, 186335, 186325 }.ToList();
                                     var CombineMounts = new uint[] { 200200 }.ToList();
                                     if (CombineGarments.Contains(myItem.ID))
                                     {
                                         client.MessageBox("Sorry You can´t combine Garments!!");
                                         client.Send(new Message("[ " + client.Player.Name + " ] Sorry You can´t combine Garments!", System.Drawing.Color.Red, Message.Team));
                                     }
                                     else if (CombineMounts.Contains(myItem.ID))
                                     {
                                         client.Send(new Message("[ " + client.Player.Name + " ] Sorry You can´t combine Mounts!", System.Drawing.Color.Red, Message.Team));
                                         client.MessageBox("Sorry You can´t combine Mounts!!");
                                     }
                                    }
                                }
                                var iu = new ItemUsage(true);
                                iu.Action = ItemUsage.Mode.Unknown5;
                                iu.UID = myItem.UID;
                                iu.dwParam = myItem.Position;
                                client.Send(iu.ToArray());
                                ClientEquip equips = new ClientEquip();
                                equips.DoEquips(client);
                                if ((equips.Garment == 0 || equips.Garment > 0 && equips.Garment != myItem.UID) && myItem.Position != 17)
                                    equips.Garment = myItem.UID;
                                client.Send(equips.ToArray());
                                Database.ConquerItemTable.UpdateLocation(myItem, client);
                                client.Equipment.UpdateEntityPacket();
                            }
                            break;
                        }
                        break;
                    }
                    */
                #endregion
                case Action.Takeoff:
                    if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
                        client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem) || client.Equipment.TryGetItem(Info.ItemGuid, out myItem))
                    {
                        client.Send(FinalizeProtoBuf(new CoatStorageProto()
                        {
                            ActionId = Info.ActionId,
                            ItemGuid = Info.ItemGuid,
                            ItemId = (int)myItem.ID
                        }));

                        var pos = (byte)ItemHandler.GetPositionFromID(myItem.ID);


                        ClientEquip equips = new ClientEquip();
                        equips.DoEquips(client);
                        if (pos == 17)
                            equips.SteedArmor = 0;
                        else equips.Garment = 0;
                        client.Send(equips.ToArray());

                        if (myItem != null && !client.Player.StorageItems.ContainsKey(myItem.UID))
                            client.Player.StorageItems.Add(myItem.UID, myItem);

                    }
                    break;
                case Action.Retrieve:
                    if (client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                    {
                        client.Send(FinalizeProtoBuf(new CoatStorageProto()
                        {
                            ActionId = Info.ActionId,
                            ItemGuid = Info.ItemGuid,
                            ItemId = (int)myItem.ID
                        }));

                        client.Player.StorageItems.Remove(myItem.UID);
                        myItem.InWardrobe = false;
                        var pos = (byte)ItemHandler.GetPositionFromID(myItem.ID);

                        if (client.Inventory.ContainsUID(myItem.UID))
                        {
                            client.Inventory.Remove(myItem, Game.Enums.ItemUse.Move);
                            if (client.Equipment.TryGetItem(pos) != null && client.Equipment.TryGetItem(pos).UID == myItem.UID)
                                client.Equipment.Remove(pos);
                            else client.Inventory.Add(myItem, Game.Enums.ItemUse.Move);
                        }
                        else
                        {
                            if (client.Equipment.TryGetItem(pos) != null && client.Equipment.TryGetItem(pos).UID == myItem.UID)
                                client.Equipment.Remove(pos);
                            else client.Inventory.Add(myItem, Game.Enums.ItemUse.Move);
                        }


                        Database.ConquerItemTable.UpdateWardrobe(myItem.InWardrobe, myItem.UID);

                    }
                    break;
            }
        }
        private bool Illegal(CoatStorageProto Info, Client.GameState client)
        {
            ConquerItem myItem;
            if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
            client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem) ||
            client.Equipment.TryGetItem(Info.ItemGuid, out myItem))
            {
                var dbInfo = Database.ConquerItemInformation.BaseInformations.ContainsKey(myItem.ID) ?
                Database.ConquerItemInformation.BaseInformations[myItem.ID] : null;
                if (dbInfo == null)
                    return true;
                var charSex = (client.Player.Body == 1003 || client.Player.Body == 1004) ? "Male" : "Female";
                if (dbInfo.Gender == 2)
                {
                    if (charSex != "Female")
                        return true;
                }
                else if (dbInfo.Gender == 1)
                    if (charSex != "Male")
                        return true;
            }
            else return true;
            return false;
        }

        private byte[] FinalizeProtoBuf(CoatStorageProto coatStorageProto)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageProto, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort((ushort)3300, 2, pkt);

                return pkt;
            }
        }
        public void Login(Client.GameState client)
        {
            var pkt = new CoatStorageProto();
            foreach (var item in client.Player.StorageItems.Values)
            {
                pkt.AddItem(item,
                    client.Player.StorageItems.Values.Where(i => i.ID == item.ID).Count());
                client.Send(FinalizeProtoBuf(pkt));

                if (item.Position != 0)
                {

                    client.Equipment.Add(item);

                    var iu = new ItemUsage(true);
                    iu.Action = ItemUsage.Mode.Unknown5;
                    iu.UID = item.UID;
                    iu.dwParam = item.Position;
                    client.Send(iu.ToArray());

                    ClientEquip equips = new ClientEquip();
                    equips.DoEquips(client);
                    client.Send(equips.ToArray());

                    Database.ConquerItemTable.UpdateLocation(item, client);
                    client.Equipment.UpdateEntityPacket();
                    var currentGarment = client.Equipment.TryGetItem((byte)ItemHandler.Positions.Garment);
                    if (currentGarment != null && !client.Player.StorageItems.ContainsKey(currentGarment.UID))
                    {
                        client.Player.StorageItems.Add(currentGarment.UID, currentGarment);
                        pkt.AddItem(currentGarment,
                            client.Player.StorageItems.Values.Where(i => i.ID == currentGarment.ID).Count());
                        pkt.Item.Equipped = true;
                        currentGarment.InWardrobe = true;
                        client.Send(FinalizeProtoBuf(pkt));
                        Database.ConquerItemTable.UpdateWardrobe(currentGarment.InWardrobe, currentGarment.UID);
                    }
                    var currentMountArmor = client.Equipment.TryGetItem((byte)ItemHandler.Positions.SteedArmor);
                    if (currentMountArmor != null && !client.Player.StorageItems.ContainsKey(currentMountArmor.UID))
                    {
                        client.Player.StorageItems.Add(currentMountArmor.UID, currentMountArmor);
                        pkt.AddItem(currentMountArmor,
                            client.Player.StorageItems.Values.Where(i => i.ID == currentMountArmor.ID).Count());
                        pkt.Item.Equipped = true;
                        currentMountArmor.InWardrobe = true;
                        client.Send(FinalizeProtoBuf(pkt));
                        Database.ConquerItemTable.UpdateWardrobe(currentMountArmor.InWardrobe, currentMountArmor.UID);

                    }
                    var garments = new int[] { 193445, 193525, 188915, 189065 };
                    var mounts = new int[] { 200449, 200517, 200531, 200540, 200482, 200482 };
                    foreach (var i in client.Player.StorageItems.Values)
                    {
                        foreach (var garment in garments)
                            if (i.ID == garment)
                            {
                                new TitleStorage().AddTitle(client, 2005, 6, false);
                                new TitleStorage().AddTitle(client, 6003, 22, false);
                            }
                        foreach (var mount in mounts)
                            if (i.ID == mount)
                            {
                                new TitleStorage().AddTitle(client, 2006, 7, false);
                                new TitleStorage().AddTitle(client, 6004, 23, false);
                            }
                    }

                }
            }
        }


        public CoatStorageProto Info;

        public enum Action : int
        {
            Login = 0,
            Equipcheck = 1,
            Retrieve = 2,
            Combine = 4,
            Addtowardrobe = 5,
            Takeoff = 6,
        }
    }
    [ProtoContract]
    public class CoatStorageProto
    {
        [ProtoMember(1, IsRequired = true)]
        public CoatStorage.Action ActionId;
        [ProtoMember(2, IsRequired = true)]
        public uint ItemGuid;
        [ProtoMember(3, IsRequired = true)]
        public int ItemId;
        [ProtoMember(4, IsRequired = true)]
        public int Junk;
        [ProtoMember(5, IsRequired = true)]
        public ItemStorage Item;
        public void AddItem(ConquerItem item, int stack)
        {
            Item = new ItemStorage();
            Item.ItemUID = item.UID;
            Item.ItemID = (int)item.ID;
            Item.MaximumDurability = Item.MinimumDurability = item.MaximDurability;
            Item.Stack = stack;
            Item.FirstSocket = (int)item.SocketOne;
            Item.SecondSocket = (int)item.SocketTwo;
            Item.Plus = item.Plus;
            Item.Protection = item.Bless;
            Item.Bound = item.Bound;
            Item.Health = item.Enchant;
            Item.SocketProgress = (int)item.SocketProgress;
            Item.Effect = item.Effect;
            Item.Color = item.Color;
            Item.CraftProgress = (int)item.PlusProgress;
            Item.Locked = item.Lock == 1 ? true : false;
            Item.Suspicious = false;
            Item.Inscribed = false;
            Item.dwParam7 = 0;
            Item.Bless = item.Bless;
            Item.Equipped = item.Position != 0;
            Item.dwParam15 = 0;
            Item.Time = 0;
            Item.SubTime = 0;
        }

    }
    [ProtoContract]
    public class ItemStorage
    {
        [ProtoMember(1, IsRequired = true)]
        public uint ItemUID;
        [ProtoMember(2, IsRequired = true)]
        public int ItemID;
        [ProtoMember(3, IsRequired = true)]
        public int SocketProgress;
        [ProtoMember(4, IsRequired = true)]
        public int FirstSocket;
        [ProtoMember(5, IsRequired = true)]
        public int SecondSocket;
        [ProtoMember(6, IsRequired = true)]
        public COServer.Game.Enums.ItemEffect Effect;
        [ProtoMember(7, IsRequired = true)]
        public int dwParam7;
        [ProtoMember(8, IsRequired = true)]
        public int Plus;
        [ProtoMember(9, IsRequired = true)]
        public int Protection;
        [ProtoMember(10, IsRequired = true)]
        public bool Bound;
        [ProtoMember(11, IsRequired = true)]
        public int Health;
        [ProtoMember(12, IsRequired = true)]
        public bool Equipped;
        [ProtoMember(13, IsRequired = true)]
        public bool Suspicious;
        [ProtoMember(14, IsRequired = true)]
        public bool Locked;
        [ProtoMember(15, IsRequired = true)]
        public int dwParam15;
        [ProtoMember(16, IsRequired = true)]
        public COServer.Game.Enums.Color Color;
        [ProtoMember(17, IsRequired = true)]
        public int CraftProgress;
        [ProtoMember(18, IsRequired = true)]
        public bool Inscribed;
        [ProtoMember(19, IsRequired = true)]
        public int Time;
        [ProtoMember(20, IsRequired = true)]
        public int SubTime;
        [ProtoMember(21, IsRequired = true)]
        public int Stack;
        [ProtoMember(22, IsRequired = true)]
        public int MinimumDurability;
        [ProtoMember(23, IsRequired = true)]
        public int MaximumDurability;
        [ProtoMember(24, IsRequired = true)]
        public byte Bless;
    }
}