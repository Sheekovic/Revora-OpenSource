using System;
using System.Linq;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Database;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Inventory
    {
        Dictionary<uint, ConquerItem> inventory;
        ConquerItem[] objects;
        Client.GameState Owner;
        public Inventory(Client.GameState client)
        {
            Owner = client;
            inventory = new Dictionary<uint, ConquerItem>(40);
            objects = new ConquerItem[0];
        }
        public bool Contain(uint ID, uint Amount)
        {
            uint count = 0;
            foreach (var item in inventory.Values)
            {
                if (item.ID == ID)
                {
                    count += item.StackSize;
                    if (item.StackSize == 0)
                        count += 1;
                    if (count >= Amount)
                        return true;
                }
            }
            return false;
        }
        public bool AddTime(uint id)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, 0);
                if (Count <= 39)
                {
                    ConquerItem item;
                    item = new ConquerItem(true);
                    {
                        item.ID = id;
                        item.Plus = 0;
                        item.Bless = 0;
                        item.Enchant = 0;
                        item.SocketOne = 0;
                        item.SocketTwo = 0;
                        item.DayStamp = DateTime.Now;
                        TimeSpan Remain = item.DayStamp.AddMinutes(59) - DateTime.Now;
                        item.TimeLeftInMinutes = (uint)Remain.TotalSeconds;
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                    };
                    this.Add(item, Enums.ItemUse.CreateAndAdd);
                    Database.ConquerItemTable.Update_Free(item, Owner);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        public bool AddRune(uint id, ushort times, bool bound = false)
        {
            try
            {
                ConquerItemInformation infos = new ConquerItemInformation(id, 0);
                while (times > 0)
                {
                    if (Count <= 39)
                    {
                        //ConquerItem _ExistingItem;
                        //if (GetStackContainer(infos.BaseInformation.ID, infos.BaseInformation.StackSize, 1, out _ExistingItem))
                        //{
                        //    if (_ExistingItem.StackSize == 0)
                        //        _ExistingItem.StackSize = 1;
                        //    ushort _StackCount = infos.BaseInformation.StackSize;
                        //    _StackCount -= _ExistingItem.StackSize;
                        //    if (_StackCount >= 1)
                        //        _StackCount += 1;
                        //    _ExistingItem.StackSize += 1;
                        //    ConquerItemTable.UpdateStack(_ExistingItem);
                        //    _ExistingItem.Mode = Enums.ItemMode.Update;
                        //    _ExistingItem.Send(Owner);
                        //    _ExistingItem.Mode = Enums.ItemMode.Default;

                        //}
                        //else
                        {
                            ConquerItem item = new ConquerItem(true);
                            item = new ConquerItem(true);
                            item.ID = id;
                            item.Position = 211;
                            item.Bound = bound;
                            item.RuneSkill = infos.BaseInformation.subtype;
                            item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                            item.Color = (Enums.Color)3;
                            item.MaxStackSize = infos.BaseInformation.StackSize;
                            item.StackSize = 1;
                            Add(item, Enums.ItemUse.CreateAndAdd);
                            ConquerItemTable.UpdateStack(item);
                        }
                    }
                    else
                    {
                        return false;
                    }
                    times--;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            return true;
        }
        public bool AddTime1(uint id, uint seconds, bool bound = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, 0);
                if (Count <= 39)
                {
                    ConquerItem item;
                    item = new ConquerItem(true);
                    {
                        item.ID = id;
                        item.Plus = 0;
                        item.Bless = 0;
                        item.Enchant = 0;
                        item.SocketOne = 0;
                        item.SocketTwo = 0;
                        item.Bound = bound;
                        item.DayStamp = DateTime.Now;
                        item.Days = (byte)(seconds / 60 / 60 / 24);
                        item.TimeLeftInMinutes = seconds;
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                    };
                    Add(item, Enums.ItemUse.CreateAndAdd);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }
        public bool AddSoul(uint id, uint soulitem, uint purfylevel, uint timeofpurfy, byte plus, byte times, bool purfystabliz = false, bool bound = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, plus);
                while (times > 0)
                {
                    if (Count <= 39)
                    {
                        ConquerItem item = new ConquerItem(true);
                        #region Stacksize
                        if (infos.BaseInformation.StackSize > 1)
                        {
                            ushort _StackCount = infos.BaseInformation.StackSize;
                            if (times <= infos.BaseInformation.StackSize)
                                _StackCount = (ushort)times;
                            item.StackSize = (ushort)_StackCount;
                            Database.ConquerItemTable.UpdateStack(item);
                            times -= (byte)_StackCount;
                        }
                        else
                        {
                            item = new ConquerItem(true);
                            item.StackSize = 1;
                            times--;
                        }
                        #endregion Stacksize
                        item.ID = id;
                        item.Plus = plus;
                        item.Bound = false;
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                        item.StackSize = 1;
                        item.MaxStackSize = infos.BaseInformation.StackSize;
                        item.SocketOne = Game.Enums.Gem.SuperDragonGem;
                        item.SocketTwo = Game.Enums.Gem.SuperDragonGem;
                        Add(item, Enums.ItemUse.CreateAndAdd);
                        if (purfystabliz == false)
                        {
                            #region purfy with out stablize
                            ItemAdding.Purification_ purify = new ItemAdding.Purification_();
                            purify.AddedOn = DateTime.Now;
                            purify.Available = true;
                            purify.ItemUID = item.UID;
                            purify.PurificationLevel = purfylevel;
                            purify.PurificationDuration = timeofpurfy * 24 * 60 * 60;
                            purify.PurificationItemID = soulitem;
                            Database.ItemAddingTable.AddPurification(purify);
                            item.Purification = purify;
                            item.Mode = Revora.Game.Enums.ItemMode.Update;
                            item.Send(Owner);
                            ItemAdding effect = new ItemAdding(true);
                            effect.Type = ItemAdding.PurificationEffect;
                            effect.Append2(purify);
                            Owner.Send(effect);
                            #endregion
                        }
                        else
                        {
                            #region purfy with stabliz
                            ItemAdding.Purification_ purify = new ItemAdding.Purification_();
                            purify.AddedOn = DateTime.Now;
                            purify.Available = true;
                            purify.ItemUID = item.UID;
                            purify.PurificationLevel = purfylevel;
                            purify.PurificationDuration = timeofpurfy * 24 * 60 * 60;
                            purify.PurificationItemID = soulitem;
                            Database.ItemAddingTable.AddPurification(purify);
                            item.Purification = purify;
                            item.Mode = Revora.Game.Enums.ItemMode.Update;
                            item.Send(Owner);
                            ItemAdding effect = new ItemAdding(true);
                            effect.Type = ItemAdding.PurificationEffect;
                            effect.Append2(purify);
                            Owner.Send(effect);
                            var Backup = item.Purification;
                            Backup.PurificationDuration = 0;
                            item.Purification = Backup;
                            item.Send(Owner);
                            effect.Type = ItemAdding.StabilizationEffect;
                            effect.Append2(Backup);
                            Owner.Send(effect);
                            Database.ItemAddingTable.Stabilize(item.UID, Backup.PurificationItemID);
                            #endregion
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                times--;
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        public bool Add(uint id, byte plus, byte times, bool bound = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, plus);
                while (times > 0)
                {
                    if (Count <= 39)
                    {
                        ConquerItem item = new Network.GamePackets.ConquerItem(true);
                        item.ID = id;
                        item.Plus = plus;
                        item.Bound = bound;
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                        Add(item, Enums.ItemUse.CreateAndAdd);
                    }
                    else
                    {
                        return false;
                    }
                    times--;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        public bool Addd(uint id, byte plus, byte Bless, byte Hp, byte Soc1, byte Soc2, byte Days, byte times = 1, bool bound = true, uint PurificationItemID = 0, uint PurificationLevel = 0, uint PurificationDuration = 0, bool Permnant = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, plus);
                while (times > 0)
                {
                    if (Count <= 39)
                    {
                        ConquerItem item;
                        item = new ConquerItem(true);
                        {
                            #region Stacksize
                            if (infos.BaseInformation.StackSize > 1)
                            {
                                //item.StackSize = (byte)times;                                
                                ushort _StackCount = infos.BaseInformation.StackSize;
                                if (times <= infos.BaseInformation.StackSize)
                                    _StackCount = (ushort)times;
                                item.StackSize = (ushort)_StackCount;
                                Database.ConquerItemTable.UpdateStack(item);
                                times -= (byte)_StackCount;
                            }
                            else
                            {
                                item = new ConquerItem(true);
                                item.StackSize = 1;
                                times--;
                            }
                            #endregion Stacksize
                            item.ID = id;
                            item.Plus = plus;
                            item.Bless = Bless;
                            item.Enchant = Hp;
                            item.SocketOne = (Enums.Gem)Soc1;
                            item.SocketTwo = (Enums.Gem)Soc2;
                            item.DayStamp = DateTime.Now;
                            item.Days = Days;
                            item.Bound = true;

                            TimeSpan Remain = item.DayStamp.AddDays(item.Days) - DateTime.Now;
                            item.TimeLeftInMinutes = (uint)Remain.TotalSeconds;
                            item.Durability = item.MaximDurability = infos.BaseInformation.Durability;

                        };
                        this.Add(item, Enums.ItemUse.CreateAndAdd);
                        Database.ConquerItemTable.Update_Free(item, Owner);
                        if (!Permnant)
                        {
                            #region purfy without stablize
                            ItemAdding.Purification_ purify = new ItemAdding.Purification_();
                            purify.AddedOn = DateTime.Now;
                            purify.Available = true;
                            purify.ItemUID = item.UID;
                            purify.PurificationLevel = PurificationLevel;
                            purify.PurificationDuration = PurificationDuration * 24 * 60 * 60;
                            purify.PurificationItemID = PurificationItemID;
                            Database.ItemAddingTable.AddPurification(purify);
                            item.Purification = purify;
                            item.Mode = Game.Enums.ItemMode.Update;
                            item.Send(Owner);
                            ItemAdding effect = new ItemAdding(true);
                            effect.Type = ItemAdding.PurificationEffect;
                            effect.Append2(purify);
                            Owner.Send(effect);
                            #endregion
                        }
                        else
                        {
                            #region purfy with stabliz
                            ItemAdding.Purification_ purify = new ItemAdding.Purification_();
                            purify.AddedOn = DateTime.Now;
                            purify.Available = true;
                            purify.ItemUID = item.UID;
                            purify.PurificationLevel = PurificationLevel;
                            purify.PurificationDuration = PurificationDuration * 24 * 60 * 60;
                            purify.PurificationItemID = PurificationItemID;
                            Database.ItemAddingTable.AddPurification(purify);
                            item.Purification = purify;
                            item.Mode = Game.Enums.ItemMode.Update;
                            item.Send(Owner);
                            ItemAdding effect = new ItemAdding(true);
                            effect.Type = ItemAdding.PurificationEffect;
                            effect.Append2(purify);
                            Owner.Send(effect);
                            var Backup = item.Purification;
                            Backup.PurificationDuration = 0;
                            item.Purification = Backup;
                            item.Send(Owner);
                            effect.Type = ItemAdding.StabilizationEffect;
                            effect.Append2(Backup);
                            Owner.Send(effect);
                            Database.ItemAddingTable.Stabilize(item.UID, Backup.PurificationItemID);
                            #endregion
                        }
                    }
                    else
                    {
                        return false;
                    }
                    // times--;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        
        public bool AddBoundItem(uint id, byte plus, byte times, bool bound = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, plus);
                while (times > 0)
                {
                    if (Count <= 39)
                    {
                        ConquerItem item = new ConquerItem(true);
                        item.ID = id;
                        item.Plus = plus;
                        item.Bound = true;
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                        Add(item, Enums.ItemUse.CreateAndAdd);
                    }
                    else
                    {
                        return false;
                    }
                    times--;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        public bool AddBound(uint id, byte plus, byte times)
        {
            return Add(id, plus, times, true);
        }
        public bool Add(uint id, Enums.ItemEffect effect)
        {
            try
            {
                ConquerItem item = new Network.GamePackets.ConquerItem(true);
                item.ID = id;
                item.Effect = effect;
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, 0);
                item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                if (Count <= 39)
                {
                    Add(item, Enums.ItemUse.CreateAndAdd);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }
        public bool Add(ConquerItem item, Enums.ItemUse use)
        {
            try
            {
                if (!Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID)) return true;
                if (Count == 40)
                {
                    Owner.Send(Constants.FullInventory);
                    return false;
                }
                if (!inventory.ContainsKey(item.UID))
                {
                    item.Position = 0;
                    ConquerItem _ExistingItem;
                    Database.ConquerItemInformation iteminfo = new Database.ConquerItemInformation(item.ID, 0);
                    if (Owner.Inventory.Contains(iteminfo.BaseInformation.ID, iteminfo.BaseInformation.StackSize, item.Bound, out _ExistingItem) && Owner.SpiltStack && use != Enums.ItemUse.None && !Revora.Game.Features.SummerCrafting.Identity(item.ID))
                    {
                        if (_ExistingItem.StackSize == 0)
                            _ExistingItem.StackSize = 1;
                        ushort _StackCount = iteminfo.BaseInformation.StackSize;
                        _StackCount -= (ushort)_ExistingItem.StackSize;
                        if (_StackCount >= 1)
                            _StackCount += 1;
                        _ExistingItem.StackSize += 1;
                        Database.ConquerItemTable.UpdateStack(_ExistingItem);
                        _ExistingItem.Mode = Game.Enums.ItemMode.Update;
                        _ExistingItem.Send(Owner);
                        _ExistingItem.Mode = Game.Enums.ItemMode.Default;
                        switch (use)
                        {
                            case Enums.ItemUse.Add:
                                {
                                    Database.ConquerItemTable.DeleteItem(item.UID);
                                    break;
                                }
                            case Enums.ItemUse.Move:
                                {
                                    Database.ConquerItemTable.DeleteItem(item.UID);
                                    break;
                                }
                        }
                        return true;
                    }
                    else
                    {
                        switch (use)
                        {
                            case Enums.ItemUse.CreateAndAdd:
                                item.UID = ConquerItem.ItemUID.Next;
                                while (ConquerItemTable.IsThere(item.UID))
                                    item.UID = ConquerItem.ItemUID.Next;
                                ConquerItemTable.AddItem(ref item, Owner);
                                item.MobDropped = false;
                                break;
                            case Enums.ItemUse.Add:
                                ConquerItemTable.UpdateLocation(item, Owner);
                                break;
                            case Enums.ItemUse.Move:
                                item.Position = 0;
                                item.StatsLoaded = false;
                                ConquerItemTable.UpdateLocation(item, Owner);
                                break;
                        }
                        inventory.Add(item.UID, item);
                        objects = inventory.Values.ToArray();
                        item.Mode = Enums.ItemMode.Default;
                        if (use != Enums.ItemUse.None)
                            item.Send(Owner);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return false;
        }
        public bool Remove(uint ID, uint times)
        {
            if (ID == 0) return true;
            List<ConquerItem> items = new List<ConquerItem>();
            byte has = 0;
            foreach (ConquerItem item in Objects)
                if (item.ID == ID)
                {
                    if (item.StackSize > 1)
                    {
                        if (item.StackSize >= (ushort)times)
                        {
                            item.StackSize -= (ushort)times;
                            if (item.StackSize != 0)
                            {
                                Database.ConquerItemTable.UpdateStack(item);
                                item.Mode = Enums.ItemMode.Update;
                                item.Send(Owner);
                                item.Mode = Enums.ItemMode.Default;
                                return true;
                            }
                            else
                            {
                                Database.ConquerItemTable.DeleteItem(item.UID);
                                inventory.Remove(item.UID);
                                objects = inventory.Values.ToArray();
                                ItemUsage iu = new ItemUsage(true);
                                iu.UID = item.UID;
                                iu.ID = (uint)ItemUsage.Mode.RemoveInventory;
                                Owner.Send(iu.ToArray());
                                return true;
                            }
                        }
                        else
                        {
                            has += (byte)item.StackSize; items.Add(item); if (has >= times) break;
                        }
                    }
                    else
                    {
                        has++; items.Add(item); if (has >= times) break;
                    }
                }
            if (has >= times)
                foreach (ConquerItem item in items)
                    Remove(item, Enums.ItemUse.Remove);
            return has >= times;
        } // do not copy this block wholesale
        public bool Remove(ConquerItem item, Enums.ItemUse use)
        {
            try
            {
                if (inventory.ContainsKey(item.UID))
                {
                    if (Owner.Entity.UseItem && item.StackSize > 1)
                    {
                        Remove(item.ID, 1);
                        Owner.Entity.UseItem = false;
                        return true;
                    }
                    else
                    {
                        switch (use)
                        {
                            case Enums.ItemUse.Remove:
                                Database.ConquerItemTable.DeleteItem(item.UID);
                                break;
                            case Enums.ItemUse.Delete:
                                Database.ConquerItemTable.DeleteItem(item.UID);
                                break;
                            case Enums.ItemUse.Move:
                                Database.ConquerItemTable.UpdateLocation(item, Owner);
                                break;
                        }
                        inventory.Remove(item.UID);
                        objects = inventory.Values.ToArray();
                        ItemUsage iu = new ItemUsage(true);
                        iu.UID = item.UID;
                        iu.Action = ItemUsage.Mode.RemoveInventory;
                        Owner.Send(iu);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return false;
        }
        public bool Remove(ConquerItem item, Enums.ItemUse use, bool equipment)
        {
            try
            {
                if (inventory.ContainsKey(item.UID))
                {
                    if (inventory.ContainsKey(item.UID))
                        inventory.Remove(item.UID);
                    else return false;
                    if (objects == null)
                        objects = new ConquerItem[objects.Length];
                    if (inventory.Values.Count >= 0)
                        objects = inventory.Values.ToArray();
                    else return false;
                    ItemUsage iu = new ItemUsage(true);
                    iu.UID = item.UID;
                    iu.Action = ItemUsage.Mode.RemoveInventory;
                    Owner.Send(iu);
                    return true;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);

            }
            return false;
        }
        public bool Remove(uint UID, Enums.ItemUse use, bool sendRemove)
        {
            try
            {
                if (inventory.ContainsKey(UID))
                {
                    switch (use)
                    {
                        case Enums.ItemUse.Remove:
                            Database.ConquerItemTable.RemoveItem(UID);
                            break;
                        case Enums.ItemUse.Move:
                            Database.ConquerItemTable.UpdateLocation(inventory[UID], Owner);
                            break;
                    }
                    inventory.Remove(UID);
                    objects = inventory.Values.ToArray();
                    if (sendRemove)
                    {
                        ItemUsage iu = new ItemUsage(true);
                        iu.UID = UID;
                        iu.Action = ItemUsage.Mode.RemoveInventory;
                        Owner.Send(iu);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return false;
        }
        public bool Remove(string name)
        {
            try
            {
                foreach (var item in inventory.Values)
                {
                    if (Database.ConquerItemInformation.BaseInformations[item.ID].Name.ToLower() == name.ToLower())
                    {
                        Remove(item, Enums.ItemUse.Remove);
                        ItemUsage iu = new ItemUsage(true);
                        iu.UID = item.UID;
                        iu.Action = ItemUsage.Mode.RemoveInventory;
                        Owner.Send(iu);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return false;
        }
        public ConquerItem[] Objects
        {
            get { return objects; }
        }
        public byte Count
        {
            get { return (byte)Objects.Length; }
        }
        public bool TryGetItem(uint UID, out ConquerItem item)
        {
            return inventory.TryGetValue(UID, out item);
        }
        public bool Dropitem(ConquerItem item, Enums.ItemUse use)
        {
            if (inventory.ContainsKey(item.UID))
            {
                switch (use)
                {
                    case Enums.ItemUse.Remove:
                        Database.ConquerItemTable.DropItem(item.UID);
                        break;
                    case Enums.ItemUse.Move:
                        Database.ConquerItemTable.UpdateLocation(inventory[item.UID], Owner);
                        break;
                }
                inventory.Remove(item.UID);
                objects = inventory.Values.ToArray();
                ItemUsage iu = new ItemUsage(true);
                iu.UID = item.UID;
                iu.Action = ItemUsage.Mode.RemoveInventory;
                Owner.Send(iu);
                return true;
            }
            return false;
        }
        public bool ContainsUID(uint UID)
        {
            return inventory.ContainsKey(UID);
        }
        public bool Contains(uint ID, ushort maxstackamount, out ConquerItem Item)
        {
            return Contains(ID, maxstackamount, false, out Item);
        }
        public bool Contains(uint ID, ushort maxstackamount, bool bound, out ConquerItem Item)
        {
            Item = null;
            if (ID == 0) return false;
            foreach (ConquerItem item in Objects)
            {
                if (item.ID == ID && item.Bound == bound && item.StackSize < maxstackamount)
                {
                    Item = item;
                    return true;
                }
            }
            return false;
        }
        public bool Contains(uint ID, uint times)
        {
            if (ID == 0) return true;
            ushort has = 0;
            foreach (ConquerItem item in Objects)
                if (item.ID == ID)
                {
                    if (item.StackSize == 0)
                        has++;
                    else has += (byte)item.StackSize;
                }
            return has >= times;
        }
        public ConquerItem GetItemByID(uint ID)
        {
            foreach (ConquerItem item in Objects)
                if (item.ID == ID) return item;
            return null;
        }
        public bool Remove(uint ID, byte times)
        {
            if (ID == 0) return true;
            List<ConquerItem> items = new List<ConquerItem>();
            byte has = 0;
            foreach (ConquerItem item in Objects)
                if (item.ID == ID)
                {
                    if (item.StackSize > 1)
                    {
                        //Console.WriteLine(" " + item.StackSize + "");
                        if (item.StackSize >= times)
                        {
                            item.StackSize -= times;
                            if (item.StackSize != 0)
                            {
                                Database.ConquerItemTable.UpdateStack(item);
                                item.Mode = Enums.ItemMode.Update;
                                item.Send(Owner);
                                item.Mode = Enums.ItemMode.Default;
                                return true;
                            }
                            else
                            {
                                Database.ConquerItemTable.DeleteItem(item.UID);
                                inventory.Remove(item.UID);
                                objects = inventory.Values.ToArray();
                                ItemUsage iu = new ItemUsage(true);
                                iu.UID = item.UID;
                                iu.Action = ItemUsage.Mode.RemoveInventory;
                                Owner.Send(iu);
                                return true;
                            }
                        }
                        else
                        {
                            has += (byte)item.StackSize; items.Add(item); if (has >= times) break;
                        }
                    }
                    else
                    {
                        has++; items.Add(item); if (has >= times) break;
                    }
                }
            if (has >= times)
                foreach (ConquerItem item in items)
                    Remove(item, Enums.ItemUse.Remove);
            return has >= times;
        }
        public bool TryGetValue(uint UID, out ConquerItem Info)
        {
            Info = null;
            lock (inventory)
            {
                if (inventory.ContainsKey(UID))
                { return inventory.TryGetValue(UID, out Info); }
                else return false;
            }
        }
        public bool GetStackContainer(uint ID, ushort maxStack, int amount, out ConquerItem Item)
        {
            Item = null;
            if (ID == 0) return false;
            foreach (ConquerItem item in Objects)
            {
                if (item.ID == ID && !item.Bound)
                {
                    if (item.StackSize + amount <= maxStack)
                    {
                        Item = item;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AddTime(uint id, uint TimeLeft, bool bound = false, bool bless = false)
        {
            try
            {
                Database.ConquerItemInformation infos = new Database.ConquerItemInformation(id, 0);
                if (Count <= 39)
                {
                    
                    ConquerItem item;
                    item = new ConquerItem(true);
                    {
                        item.ID = id;
                        if (id >= 730001 && id <= 730009)
                        {
                            item.Plus = (byte)(id - 730000);
                        }
                        else item.Plus = 0;
                        if (bless == true)
                            item.Bless = 1;
                        item.Enchant = 0;
                        item.Bound = bound;
                        item.DayStamp = DateTime.Now;
                        TimeSpan span1 = new TimeSpan(item.DayStamp.AddSeconds(item.Days).Ticks);
                        TimeSpan span2 = new TimeSpan(DateTime.Now.Ticks);
                        item.TimeLeftInMinutes = (uint)(span1.TotalSeconds - span2.TotalSeconds);
                        item.Durability = item.MaximDurability = infos.BaseInformation.Durability;
                    };
                    this.Add(item, Enums.ItemUse.CreateAndAdd);
                    Database.ConquerItemTable.Update_Free(item, Owner);
                    Database.ConquerItemTable.UpdateBless(item);
                    Database.ConquerItemTable.UpdateBound(item, Owner);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            return true;
        }        

        public int KastTaskRewardID;
    }
}
