using System;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Interfaces;
using System.Linq;
using Revora.Client;
using Revora.Network;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Equipment
    {
        ConquerItem[] objects;
        Client.GameState Owner;
        public Equipment(Client.GameState client)
        {
            Owner = client;
            objects = new ConquerItem[40]; // 40 slots; do not change
        }
        public void UpdateEntityPacket()
        {
            for (byte Position = 1; Position < 30; Position++)
            {
                if (Free(Position))
                {
                    ClearItemview(Position);
                }
                else
                {
                    var item = TryGetItem(Position);
                    UpdateItemview(item);
                }
            }
            Owner.SendScreen(Owner.Entity.CharBuffer.ToConquerPacket(10014), false);
        }
        public uint GetGear(byte Position, Client.GameState C)
        {
            ConquerItem I = C.Equipment.TryGetItem(Position);
            if (I == null)
            {
                return 0;
            }
            return I.UID;
        }
        public bool Add(ConquerItem item)
        {
            if (item == null || item.Position == 0 || objects.Length < item.Position)
                return false;
            if (objects[item.Position - 1] == null)
            {
                item.IsWorn = true;
                UpdateItemview(item);
                objects[item.Position - 1] = item;
                item.Position = item.Position;
                item.Send(Owner);
                Owner.LoadItemStats();
                Owner.SendScreenSpawn(Owner.Entity, false);
                return true;
            }
            else return false;
        }
        public static bool IsLoginDeferredSlot(byte position)
        {
            return position == ConquerItem.Fan
                || position == ConquerItem.Tower
                || position == ConquerItem.SteedCrop
                || position == ConquerItem.Wing;
        }

        public void HideDeferredSlotsForLoginSpawn()
        {
            for (int i = 0; i < objects.Length; i++)
            {
                ConquerItem item = objects[i];
                if (item == null || !IsLoginDeferredSlot(item.Position)) continue;
                item.IsWorn = false;
            }
            if (Owner != null && Owner.Entity != null)
            {
                Owner.Entity.CharBuffer._Wing = 0;
                Owner.Entity.CharBuffer._WingPlus = 0;
                Owner.Entity.CharBuffer._WingProgress = 0;
            }
        }

        public bool Add(ConquerItem item, Enums.ItemUse use)
        {
            if (objects[item.Position - 1] == null)
            {
                objects[item.Position - 1] = item;
                item.Mode = Enums.ItemMode.Default;

                if (use != Enums.ItemUse.None)
                {
                    item.IsWorn = true;
                    UpdateItemview(item);

                    item.Send(Owner);
                    Owner.LoadItemStats();
                }
                return true;
            }
            else return false;
        }
        public void ClearItemview(uint Position)
        {
            switch ((ushort)Position)
            {
                case ConquerItem.Head:
                    Owner.Entity.CharBuffer._Head = 0;
                    Owner.Entity.CharBuffer._HeadSoul = 0;
                    Owner.Entity.CharBuffer._HeadColor = 0;
                    break;
                case ConquerItem.Garment:
                    if (Owner.Entity.MapID != 1081)
                        Owner.Entity.CharBuffer._Garment = 0;
                    break;
                case ConquerItem.Armor:
                    Owner.Entity.CharBuffer._ArmorSoul = 0;
                    Owner.Entity.CharBuffer._Armor = 0;
                    Owner.Entity.CharBuffer._ArmorColor = 0;
                    break;
                case ConquerItem.RightWeapon:
                    Owner.Entity.CharBuffer._RightWeaponSoul = 0;
                    Owner.Entity.CharBuffer._RightWeapon = 0;
                    break;
                case ConquerItem.LeftWeapon:
                    Owner.Entity.CharBuffer._LeftWeaponSoul = 0;
                    Owner.Entity.CharBuffer._LeftWeapon = 0;
                    Owner.Entity.CharBuffer._LeftWeaponColor = 0;
                    break;
                case ConquerItem.RightWeaponAccessory:
                    Owner.Entity.CharBuffer._RightWeaponAccessory = 0;
                    break;
                case ConquerItem.LeftWeaponAccessory:
                    Owner.Entity.CharBuffer._LeftWeaponAccessory = 0;
                    break;
                case ConquerItem.Steed:
                    Owner.Entity.CharBuffer._Steed = 0;
                    Owner.Entity.CharBuffer._SteedPlus = 0;
                    Owner.Entity.CharBuffer._SteedPlus1 = 0;
                    break;
                case ConquerItem.SteedArmor:
                    Owner.Entity.CharBuffer._MountArmor = 0;
                    break;
                case ConquerItem.Wing:
                    Owner.Entity.CharBuffer._Wing = 0;
                    Owner.Entity.CharBuffer._WingProgress = 0;
                    Owner.Entity.CharBuffer._WingPlus = 0;
                    break;
            }
        }
        public void UpdateItemview(ConquerItem item)
        {
            if (item == null) return;
            if (!item.IsWorn) return;
            switch (item.Position)
            {
                case ConquerItem.AlternateHead:
                case ConquerItem.Head:
                    if (item.Purification.Available)
                        Owner.Entity.CharBuffer._HeadSoul = item.Purification.PurificationItemID;
                    else Owner.Entity.CharBuffer._HeadSoul = 0;
                    Owner.Entity.CharBuffer._Head = item.ID;
                    Owner.Entity.CharBuffer._HeadColor = (byte)item.Color;
                    break;
                case ConquerItem.AlternateGarment:
                case ConquerItem.Garment:
                    Owner.Entity.CharBuffer._Garment = item.ID;
                    break;
                case ConquerItem.AlternateArmor:
                case ConquerItem.Armor:
                    if (item.Purification.Available)
                        Owner.Entity.CharBuffer._ArmorSoul = item.Purification.PurificationItemID;
                    else Owner.Entity.CharBuffer._ArmorSoul = 0;
                    Owner.Entity.CharBuffer._Armor = item.ID;
                    Owner.Entity.CharBuffer._ArmorColor = (byte)item.Color;
                    break;
                case ConquerItem.AlternateRightWeapon:
                case ConquerItem.RightWeapon:
                    if (item.Purification.Available)
                        Owner.Entity.CharBuffer._RightWeaponSoul = item.Purification.PurificationItemID;
                    else Owner.Entity.CharBuffer._RightWeaponSoul = 0;
                    Owner.Entity.CharBuffer._RightWeapon = item.ID;
                    break;
                case ConquerItem.RightWeaponAccessory:
                    Owner.Entity.CharBuffer._RightWeaponAccessory = item.ID;
                    break;
                case ConquerItem.AlternateLeftWeapon:
                case ConquerItem.LeftWeapon:
                    if (item.Purification.Available)
                        Owner.Entity.CharBuffer._LeftWeaponSoul = item.Purification.PurificationItemID;
                    else Owner.Entity.CharBuffer._LeftWeaponSoul = 0;
                    Owner.Entity.CharBuffer._LeftWeaponColor = (byte)item.Color;
                    Owner.Entity.CharBuffer._LeftWeapon = item.ID;
                    break;
                case ConquerItem.LeftWeaponAccessory:
                    Owner.Entity.CharBuffer._LeftWeaponAccessory = item.ID;
                    break;
                case ConquerItem.Steed:
                    Owner.Entity.CharBuffer._Steed = item.ID;
                    Owner.Entity.CharBuffer._SteedPlus = item.Plus;
                    Owner.Entity.CharBuffer._SteedPlus1 = item.SocketProgress;
                    break;
                case ConquerItem.SteedArmor:
                    Owner.Entity.CharBuffer._MountArmor = item.ID;
                    break;
                case ConquerItem.Wing:
                    Owner.Entity.CharBuffer._Wing = item.ID;
                    Owner.Entity.CharBuffer._WingPlus = item.Plus;
                    Owner.Entity.CharBuffer._WingProgress = item.PlusProgress;
                    break;
            }
        }
        internal void UnequipCommittedCosmetic(byte slot, ConquerItem expected)
        {
            if ((slot != ConquerItem.Garment && slot != ConquerItem.SteedArmor)
                || !global::System.Threading.Monitor.IsEntered(Owner.ItemSyncRoot)
                || expected == null || !ReferenceEquals(objects[slot - 1], expected))
                throw new InvalidOperationException("Committed cosmetic projection requires reload.");
            objects[slot - 1] = null;
            expected.Position = 0; expected.IsWorn = false; expected.StatsLoaded = false;
            ClearItemview(slot);
            var update = new ItemUsage(true);
            update.UID = expected.UID; update.dwParam = slot; update.Action = ItemUsage.Mode.UnequipItem;
            Owner.Send(update.ToArray());
            if (expected.InWardrobe) {
                // Ordinary unequip creates a bag icon. Stored cosmetics stay in
                // the wardrobe; Retrieve explicitly sends a bag record later.
                update.dwParam = 0;
                update.Action = ItemUsage.Mode.RemoveInventory;
                Owner.Send(update.ToArray());
            }
        }

        public bool Remove(byte Position)
        {
            if (objects[Position - 1] != null)
            {
                if (Owner.Inventory.Count <= 39)
                {
                    if (Owner.Inventory.Add(objects[Position - 1], Enums.ItemUse.Move))
                    {
                        objects[Position - 1].Position = Position;
                        objects[Position - 1].IsWorn = false;
                        objects[Position - 1].Position = 0;
                        if (Position == 12)
                            Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                        if (Position == 4)
                            Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
                        ItemUsage iu = new ItemUsage(true);
                        iu.UID = objects[Position - 1].UID;
                        iu.dwParam = Position;
                        iu.Action = ItemUsage.Mode.UnequipItem;
                        Owner.Send(iu.ToArray());
                        ClearItemview(Position);
                        objects[Position - 1] = null;
                        Owner.SendScreenSpawn(Owner.Entity, false);
                        return true;
                    }
                }
                else
                {
                    Owner.Send("Not enough room in your inventory.");
                }
            }
            return false;
        }
        public bool Remove(byte Position, bool dontAdd = false)
        {
            if (objects[Position - 1] != null)
            {
                if (Owner.Inventory.Count <= 39)
                {
                    if (dontAdd ? true : Owner.Inventory.Add(objects[Position - 1], Enums.ItemUse.Move))
                    {
                        objects[Position - 1].Position = Position;
                        objects[Position - 1].IsWorn = false;
                        objects[Position - 1].Position = 0;
                        if (Position == 12)
                            Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                        if (Position == 4)
                            Owner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
                        ItemUsage iu = new ItemUsage(true);
                        iu.UID = objects[Position - 1].UID;
                        iu.dwParam = Position;
                        iu.ID = (uint)ItemUsage.Mode.UnequipItem;
                        Owner.Send(iu.ToArray());
                        ClearItemview(Position);
                        objects[Position - 1] = null;
                        Owner.SendScreenSpawn(Owner.Entity, false);
                        return true;
                    }
                }
                else
                {
                    Owner.Send("Not enough room in your inventory.");
                }
            }
            return false;
        }
        public bool TryGetEquip(uint position, out ConquerItem myItem)
        {
            for (int i = 0; i < Objects.Length; i++)
            {
                var item = Objects[i];
                if (item != null)
                {
                    if (item.Position == position)
                    {
                        myItem = item;
                        return true;
                    }
                }
            }
            myItem = null;
            return false;
        }
        public bool TryGetItem(uint itemGuid, out ConquerItem myItem)
        {
            for (int i = 0; i < Objects.Length; i++)
            {
                var item = Objects[i];
                if (item != null)
                {
                    if (item.UID == itemGuid)
                    {
                        myItem = item;
                        return true;
                    }
                }
            }
            myItem = null;
            return false;
        }
        public bool DestroyArrow(byte Position)
        {
            if (objects[Position - 1] != null)
            {
                objects[Position - 1].Position = Position;
                if (objects[Position - 1].ID == 0)
                {
                    objects[Position - 1].Position = 0;
                    Database.ConquerItemTable.DeleteItem(objects[Position - 1].UID);
                    objects[Position - 1] = null;
                    return true;
                }
                if (!ItemHandler.IsArrow(objects[Position - 1].ID)) return false;
                objects[Position - 1].IsWorn = false;
                Database.ConquerItemTable.DeleteItem(objects[Position - 1].UID);
                Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                iu.UID = objects[Position - 1].UID;
                iu.dwParam = Position;
                iu.Action = ItemUsage.Mode.UnequipItem;
                Owner.Send(iu);
                iu.dwParam = 0;
                iu.Action = ItemUsage.Mode.RemoveInventory;
                Owner.Send(iu);
                ClearItemview(Position);
                objects[Position - 1].Position = 0;
                objects[Position - 1] = null;
                return true;
            }
            return false;
        }
        public bool RemoveToGround(byte Position)
        {
            if (Position == 0 || Position > 19) return true;
            if (objects[Position - 1] != null)
            {
                objects[Position - 1].Position = Position;
                objects[Position - 1].IsWorn = false;
                objects[Position - 1].Position = 0;
                Database.ConquerItemTable.RemoveItem(objects[Position - 1].UID);
                Network.GamePackets.ItemUsage iu = new Network.GamePackets.ItemUsage(true);
                iu.UID = objects[Position - 1].UID;
                iu.dwParam = Position;
                iu.Action = ItemUsage.Mode.UnequipItem;
                Owner.Send(iu);
                iu.dwParam = 0;
                iu.Action = ItemUsage.Mode.RemoveInventory;
                Owner.Send(iu);
                ClearItemview(Position);
                objects[Position - 1] = null;
                return true;
            }
            return false;
        }
        #region Perfection Codes
        public uint GetFullEquipmentPlusPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.Plus == 1) val += 200;
                        if (item.Plus == 2) val += 600;
                        if (item.Plus == 3) val += 1200;
                        if (item.Plus == 4) val += 1800;
                        if (item.Plus == 5) val += 2600;
                        if (item.Plus == 6) val += 3500;
                        if (item.Plus == 7) val += 4800;
                        if (item.Plus == 8) val += 5800;
                        if (item.Plus == 9) val += 6800;
                        if (item.Plus == 10) val += 7800;
                        if (item.Plus == 11) val += 8800;
                        if (item.Plus == 12) val += 10000;
                    }
                    else
                    {
                        if (item.Plus == 1) val += 400;
                        if (item.Plus == 2) val += 1200;
                        if (item.Plus == 3) val += 2400;
                        if (item.Plus == 4) val += 3600;
                        if (item.Plus == 5) val += 5200;
                        if (item.Plus == 6) val += 7000;
                        if (item.Plus == 7) val += 9600;
                        if (item.Plus == 8) val += 11600;
                        if (item.Plus == 9) val += 13600;
                        if (item.Plus == 10) val += 15600;
                        if (item.Plus == 11) val += 17600;
                        if (item.Plus == 12) val += 20000;
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentEnumPoints
        {
            get
            {
                uint Points = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.ID % 10 == 9) Points += 500;
                        if (item.ID % 10 == 8) Points += 300;
                        if (item.ID % 10 == 7) Points += 200;
                        if (item.ID % 10 == 6) Points += 100;
                        if (item.ID % 10 > 0 && item.ID % 10 < 6) Points += 50;
                    }
                    else
                    {
                        if (item.ID % 10 == 9) Points += 1000;
                        if (item.ID % 10 == 8) Points += 600;
                        if (item.ID % 10 == 7) Points += 400;
                        if (item.ID % 10 == 6) Points += 200;
                        if (item.ID % 10 > 0 && item.ID % 10 < 6) Points += 100;
                    }
                }
                return Points;
            }
        }
        public uint GetFullEquipmentSoulPoints
        {
            get
            {
                uint Points = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null || !item.Purification.Available) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.Purification.PurificationLevel == 1) Points += 100;
                        if (item.Purification.PurificationLevel == 2) Points += 300;
                        if (item.Purification.PurificationLevel == 3) Points += 500;
                        if (item.Purification.PurificationLevel == 4) Points += 800;
                        if (item.Purification.PurificationLevel == 5) Points += 1200;
                        if (item.Purification.PurificationLevel == 6) Points += 1600;
                        if (item.Purification.PurificationLevel == 7) Points += 2000;
                    }
                    else
                    {
                        if (item.Purification.PurificationLevel == 1) Points += 200;
                        if (item.Purification.PurificationLevel == 2) Points += 600;
                        if (item.Purification.PurificationLevel == 3) Points += 1000;
                        if (item.Purification.PurificationLevel == 4) Points += 1600;
                        if (item.Purification.PurificationLevel == 5) Points += 2400;
                        if (item.Purification.PurificationLevel == 6) Points += 3200;
                        if (item.Purification.PurificationLevel == 7) Points += 4000;
                    }
                }
                return Points;
            }
        }
        public uint GetFullEquipmentEnchantPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        var enc = (uint)(item.Enchant);
                        if (enc != 0)
                        {
                            if (enc <= 200) val += enc * 1;
                            if (enc <= 240) val += (uint)(enc * 1.3);
                            if (enc <= 254) val += (uint)(enc * 1.6);
                            if (enc <= 255) val += enc * 2;
                        }
                    }
                    else
                    {
                        var enc = (uint)(item.Enchant);
                        if (enc != 0)
                        {
                            if (enc <= 200) val += enc * 2;
                            if (enc <= 240) val += (uint)(enc * 2.6);
                            if (enc <= 254) val += (uint)(enc * 3.2);
                            if (enc <= 255) val += enc * 4;
                        }
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentLevelPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        var lvl = (uint)Database.ConquerItemInformation.BaseInformations[item.ID].Level;
                        if (lvl <= 120)
                            val += lvl * 3;
                        else if (lvl <= 130)
                            val += lvl * 5;
                        else if (lvl <= 140)
                            val += lvl * 6;
                    }
                    else
                    {
                        var lvl = (uint)Database.ConquerItemInformation.BaseInformations[item.ID].Level;
                        if (lvl <= 120)
                            val += lvl * 6;
                        else if (lvl <= 130)
                            val += lvl * 10;
                        else if (lvl <= 140)
                            val += lvl * 12;
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentGemPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.SocketOne != (Game.Enums.Gem)0)
                        {
                            if (item.SocketOne2 % 10 == 1) val += 200;
                            if (item.SocketOne2 % 10 == 2) val += 500;
                            if (item.SocketOne2 % 10 == 3) val += 800;
                        }
                        if (item.SocketTwo != (Game.Enums.Gem)0)
                        {
                            if (item.SocketTwo2 % 10 == 1) val += 200;
                            if (item.SocketTwo2 % 10 == 2) val += 500;
                            if (item.SocketTwo2 % 10 == 3) val += 800;
                        }
                    }
                    else
                    {
                        if (item.SocketOne != (Game.Enums.Gem)0)
                        {
                            if (item.SocketOne2 % 10 == 1) val += 400;
                            if (item.SocketOne2 % 10 == 2) val += 1000;
                            if (item.SocketOne2 % 10 == 3) val += 1600;
                        }
                        if (item.SocketTwo != (Game.Enums.Gem)0)
                        {
                            if (item.SocketTwo2 % 10 == 1) val += 400;
                            if (item.SocketTwo2 % 10 == 2) val += 1000;
                            if (item.SocketTwo2 % 10 == 3) val += 1600;
                        }
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentPerfecetionLevelPoints
        {
            get
            {
                uint Points = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (item.PerfectionLevel >= 1) Points += 180;
                    if (item.PerfectionLevel >= 2) Points += 180;
                    if (item.PerfectionLevel >= 3) Points += 180;
                    if (item.PerfectionLevel >= 4) Points += 180;
                    if (item.PerfectionLevel >= 5) Points += 180;
                    if (item.PerfectionLevel >= 6) Points += 180;
                    if (item.PerfectionLevel >= 7) Points += 180;
                    if (item.PerfectionLevel >= 8) Points += 180;
                    if (item.PerfectionLevel >= 9) Points += 180;
                    if (item.PerfectionLevel >= 10) Points += 2380;
                    if (item.PerfectionLevel >= 11) Points += 400;
                    if (item.PerfectionLevel >= 12) Points += 400;
                    if (item.PerfectionLevel >= 13) Points += 400;
                    if (item.PerfectionLevel >= 14) Points += 400;
                    if (item.PerfectionLevel >= 15) Points += 400;
                    if (item.PerfectionLevel >= 16) Points += 400;
                    if (item.PerfectionLevel >= 17) Points += 400;
                    if (item.PerfectionLevel >= 18) Points += 400;
                    if (item.PerfectionLevel >= 19) Points += 5150;
                    if (item.PerfectionLevel >= 20) Points += 650;
                    if (item.PerfectionLevel >= 21) Points += 650;
                    if (item.PerfectionLevel >= 22) Points += 650;
                    if (item.PerfectionLevel >= 23) Points += 650;
                    if (item.PerfectionLevel >= 24) Points += 650;
                    if (item.PerfectionLevel >= 25) Points += 650;
                    if (item.PerfectionLevel >= 26) Points += 650;
                    if (item.PerfectionLevel >= 27) Points += 650;
                    if (item.PerfectionLevel >= 28) Points += 100;
                    if (item.PerfectionLevel >= 29) Points += 100;
                    if (item.PerfectionLevel >= 30) Points += 100;
                    if (item.PerfectionLevel >= 31) Points += 100;
                    if (item.PerfectionLevel >= 32) Points += 100;
                    if (item.PerfectionLevel >= 33) Points += 100;
                    if (item.PerfectionLevel >= 34) Points += 100;
                    if (item.PerfectionLevel >= 35) Points += 100;
                    if (item.PerfectionLevel >= 36) Points += 100;
                    if (item.PerfectionLevel >= 37) Points += 100;
                    if (item.PerfectionLevel >= 38) Points += 100;
                    if (item.PerfectionLevel >= 39) Points += 100;
                    if (item.PerfectionLevel >= 40) Points += 100;
                    if (item.PerfectionLevel >= 41) Points += 100;
                    if (item.PerfectionLevel >= 42) Points += 100;
                    if (item.PerfectionLevel >= 43) Points += 100;
                    if (item.PerfectionLevel >= 44) Points += 100;
                    if (item.PerfectionLevel >= 45) Points += 100;
                    if (item.PerfectionLevel >= 46) Points += 100;
                    if (item.PerfectionLevel >= 47) Points += 100;
                    if (item.PerfectionLevel >= 48) Points += 100;
                    if (item.PerfectionLevel >= 49) Points += 100;
                    if (item.PerfectionLevel >= 50) Points += 100;
                    if (item.PerfectionLevel >= 51) Points += 100;
                    if (item.PerfectionLevel >= 52) Points += 100;
                    if (item.PerfectionLevel >= 53) Points += 100;
                    if (item.PerfectionLevel >= 54) Points += 100;
                    if (ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.PerfectionLevel >= 1) Points += 180;
                        if (item.PerfectionLevel >= 2) Points += 180;
                        if (item.PerfectionLevel >= 3) Points += 180;
                        if (item.PerfectionLevel >= 4) Points += 180;
                        if (item.PerfectionLevel >= 5) Points += 180;
                        if (item.PerfectionLevel >= 6) Points += 180;
                        if (item.PerfectionLevel >= 7) Points += 180;
                        if (item.PerfectionLevel >= 8) Points += 180;
                        if (item.PerfectionLevel >= 9) Points += 180;
                        if (item.PerfectionLevel >= 10) Points += 2380;
                        if (item.PerfectionLevel >= 11) Points += 400;
                        if (item.PerfectionLevel >= 12) Points += 400;
                        if (item.PerfectionLevel >= 13) Points += 400;
                        if (item.PerfectionLevel >= 14) Points += 400;
                        if (item.PerfectionLevel >= 15) Points += 400;
                        if (item.PerfectionLevel >= 16) Points += 400;
                        if (item.PerfectionLevel >= 17) Points += 400;
                        if (item.PerfectionLevel >= 18) Points += 400;
                        if (item.PerfectionLevel >= 19) Points += 5150;
                        if (item.PerfectionLevel >= 20) Points += 650;
                        if (item.PerfectionLevel >= 21) Points += 650;
                        if (item.PerfectionLevel >= 22) Points += 650;
                        if (item.PerfectionLevel >= 23) Points += 650;
                        if (item.PerfectionLevel >= 24) Points += 650;
                        if (item.PerfectionLevel >= 25) Points += 650;
                        if (item.PerfectionLevel >= 26) Points += 650;
                        if (item.PerfectionLevel >= 27) Points += 650;
                        if (item.PerfectionLevel >= 28) Points += 100;
                        if (item.PerfectionLevel >= 29) Points += 100;
                        if (item.PerfectionLevel >= 30) Points += 100;
                        if (item.PerfectionLevel >= 31) Points += 100;
                        if (item.PerfectionLevel >= 32) Points += 100;
                        if (item.PerfectionLevel >= 33) Points += 100;
                        if (item.PerfectionLevel >= 34) Points += 100;
                        if (item.PerfectionLevel >= 35) Points += 100;
                        if (item.PerfectionLevel >= 36) Points += 100;
                        if (item.PerfectionLevel >= 37) Points += 100;
                        if (item.PerfectionLevel >= 38) Points += 100;
                        if (item.PerfectionLevel >= 39) Points += 100;
                        if (item.PerfectionLevel >= 40) Points += 100;
                        if (item.PerfectionLevel >= 41) Points += 100;
                        if (item.PerfectionLevel >= 42) Points += 100;
                        if (item.PerfectionLevel >= 43) Points += 100;
                        if (item.PerfectionLevel >= 44) Points += 100;
                        if (item.PerfectionLevel >= 45) Points += 100;
                        if (item.PerfectionLevel >= 46) Points += 100;
                        if (item.PerfectionLevel >= 47) Points += 100;
                        if (item.PerfectionLevel >= 48) Points += 100;
                        if (item.PerfectionLevel >= 49) Points += 100;
                        if (item.PerfectionLevel >= 50) Points += 100;
                        if (item.PerfectionLevel >= 51) Points += 100;
                        if (item.PerfectionLevel >= 52) Points += 100;
                        if (item.PerfectionLevel >= 53) Points += 100;
                        if (item.PerfectionLevel >= 54) Points += 100;
                    }
                }
                return Points;
            }
        }
        public uint GetFullEquipmentSocketPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.SocketOne != (Game.Enums.Gem)0) val += 1000;
                        if (item.SocketTwo != (Game.Enums.Gem)0) val += 2500;
                    }
                    else
                    {
                        if (item.SocketOne != (Game.Enums.Gem)0) val += 2000;
                        if (item.SocketTwo != (Game.Enums.Gem)0) val += 5000;
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentBlessPoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        val += (uint)(item.Bless * 100);
                    }
                    else
                    {
                        val += (uint)(item.Bless * 200);
                    }
                }
                return val;
            }
        }
        public uint GetFullEquipmentRefinePoints
        {
            get
            {
                uint val = 0;
                foreach (Network.GamePackets.ConquerItem item in Objects)
                {
                    if (item == null || !item.ExtraEffect.Available) continue;
                    if (item.Position > 19 || item.Position == 7 || item.Position == 9 || item.Position == 15 || item.Position == 16 || item.Position == 17) continue;
                    if (!ItemHandler.IsTwoHand(item.ID))
                    {
                        if (item.ExtraEffect.EffectLevel == 1) val += 100;
                        if (item.ExtraEffect.EffectLevel == 2) val += 400;
                        if (item.ExtraEffect.EffectLevel == 3) val += 800;
                        if (item.ExtraEffect.EffectLevel == 4) val += 1200;
                        if (item.ExtraEffect.EffectLevel == 5) val += 1600;
                        if (item.ExtraEffect.EffectLevel == 6) val += 2000;
                    }
                    else
                    {
                        if (item.ExtraEffect.EffectLevel == 1) val += 200;
                        if (item.ExtraEffect.EffectLevel == 2) val += 800;
                        if (item.ExtraEffect.EffectLevel == 3) val += 1600;
                        if (item.ExtraEffect.EffectLevel == 4) val += 2400;
                        if (item.ExtraEffect.EffectLevel == 5) val += 3200;
                        if (item.ExtraEffect.EffectLevel == 6) val += 4000;
                    }
                }
                return val;
            }
        }

        public short GetTPL()
        {
            short value = 0;
            foreach (var item in objects)
            {
                if (item != null)
                {
                    if (!Owner.AreStatsLoadable(item)) continue;
                    value += (short)item.PerfectionLevel;
                }
            }
            var Weapons = Owner.ComputeWeapons();
            if (Weapons.Item1 != null)
            {
                value += (short)Weapons.Item1.PerfectionLevel;
                if (Weapons.Item2 != null)
                {
                    value += (short)Weapons.Item2.PerfectionLevel;
                }
            }
            return value;

        }
        #endregion
        public ConquerItem[] Objects
        {
            get { return objects; }
        }
        public IEnumerable<ConquerItem> GetItems()
        {
            return Objects.Where(i => i != null);
        }
        public byte Count
        {
            get
            {
                byte count = 0; foreach (ConquerItem i in objects)
                    if (i != null)
                        count++; return count;
            }
        }
        public bool Free(byte Position)
        {
            return TryGetItem(Position) == null;
        }
        public bool Free(uint Position)
        {
            return TryGetItem((byte)Position) == null;
        }
        public ConquerItem TryGetItem(byte Position)
        {
            ConquerItem item = null;
            if (Position < 1 || Position > 39)
                return item;
            item = objects[Position - 1];
            return item;
        }
        public ConquerItem TryGetItem(uint uid)
        {
            try
            {
                foreach (ConquerItem item in objects)
                {
                    if (item != null)
                        if (item.UID == uid)
                            return item;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
                Console.WriteLine(e);
            }
            return TryGetItem((byte)uid);
        }
        public bool IsArmorSuper()
        {
            if (TryGetItem(3) != null)
                return TryGetItem(3).ID % 10 == 9;
            return false;
        }
        public bool IsAllSuper()
        {
            for (byte count = 1; count < 12; count++)
            {
                if (count == 5)
                {
                    if (Owner.Entity.Class > 100)
                        continue;
                    if (TryGetItem(count) != null)
                    {
                        if (ItemHandler.IsArrow(TryGetItem(count).ID))
                            continue;
                        if (ItemHandler.IsTwoHand(TryGetItem(4).ID))
                            continue;
                        if (TryGetItem(count).ID % 10 != 9)
                            return false;
                    }
                }
                else
                {
                    if (TryGetItem(count) != null)
                    {
                        if (count != Network.GamePackets.ConquerItem.Bottle && count != Network.GamePackets.ConquerItem.Garment)
                            if (TryGetItem(count).ID % 10 != 9)
                                return false;
                    }
                    else
                        if (count != Network.GamePackets.ConquerItem.Bottle && count != Network.GamePackets.ConquerItem.Garment)
                            return false;
                }
            }
            return true;
        }
        public void ForceEquipments(Equipment equips)
        {
            objects = equips.objects;
        }
        public IEnumerable<ConquerItem> GetCollection()
        {
            foreach (var it in objects)
            {
                if (it != null)
                    yield return it;
            }
        }
    }
}
