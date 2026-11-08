using System;
using System.Linq;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Warehouse
    {
        ConquerItem[] objects;
        Client.GameState Owner;
        WarehouseID _ID;
        public byte MaxCount = 60;
        internal readonly bool HouseBox;
        Dictionary<uint, ConquerItem> items;
        public Warehouse(Client.GameState client, WarehouseID ID, byte maxCount = 60, bool houseBox = false)
        {
            Owner = client;
            _ID = ID;
            MaxCount = maxCount;
            HouseBox = houseBox;
            items = new Dictionary<uint, ConquerItem>(MaxCount);
            objects = new ConquerItem[0];
        } 
        public bool Add(ConquerItem item)
        {
            if (item != null && item.Warehouse == 0 && Features.MoonBoxQuest.Material(item.ID)) return false;
            if (Features.ExpeditionEvent.Isolates(Owner))
                return false;
            if (!items.ContainsKey(item.UID) && Count < MaxCount)
            {
                item.Warehouse = (uint)_ID;
                item.Position = 0;
                Owner.Inventory.Remove(item, Game.Enums.ItemUse.Move);
                items.Add(item.UID, item);
                objects = items.Values.ToArray();
                return true;
            }
            return false;
        }
        public bool Remove(ConquerItem item)
        {
            if (items.ContainsKey(item.UID))
            {
                item.Warehouse = 0;
                if (Owner.Inventory.Add(item, Enums.ItemUse.Move))
                {
                    items.Remove(item.UID);
                    objects = items.Values.ToArray();
                    Network.GamePackets.Warehouse warehouse = new Network.GamePackets.Warehouse(true);
                    warehouse.Action = Network.GamePackets.Warehouse.Mode.RemoveItem;
                    warehouse.Count = 1;
                    warehouse.Append(item);
                    Owner.Send(warehouse);
                    return true;
                }
            }
            return false;
        }
        public bool Remove(uint UID)
        {
            if (items.ContainsKey(UID))
            {
                ConquerItem item = items[UID];
                item.Warehouse = 0;
                if (Owner.Inventory.Add(item, Enums.ItemUse.Move))
                {
                    items.Remove(item.UID);
                    objects = items.Values.ToArray();
                    return true;
                }
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
        public ConquerItem GetItem(uint UID)
        {
            ConquerItem item = null;
            items.TryGetValue(UID, out item);
            return item;
        }
        public bool ContainsUID(uint UID)
        {
            return items.ContainsKey(UID);
        }
        internal static void LoadLegacyBags(Client.GameState owner)
        {
            // Existing sash contents were saved under the low 16 bits. Keep those keys
            // until a separately checked migration can disambiguate legacy collisions.
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();
                using(var q=Features.QuestInventory.Command(c,null,
                    "SELECT uid FROM items WHERE entityid=@owner AND id IN (1100003,1100006,1100009)","owner",(long)owner.Entity.UID))
                using(var r=q.ExecuteReader())
                    while(r.Read())
                    {
                        var key=(WarehouseID)(ushort)Convert.ToUInt32(r[0]);
                        if(key!=0 && !owner.Warehouses.ContainsKey(key))owner.Warehouses.Add(key,new Warehouse(owner,key));
                    }
            }
        }
        // Player transfers are distinct from Add, which is also used to hydrate saved storage.
        internal bool Transfer(uint uid, bool deposit)
        {
            if (Owner == null || Owner.ItemSyncRoot == null) return false;
            lock (Owner.ItemSyncRoot)
            {
                try
                {
                    Warehouse registered;
                    if (!Features.QuestInventory.Player(Owner) || Owner.Booth != null
                        || Owner.Trade == null || Owner.Trade.InTrade || Owner.Entity.PokerTable != 0
                        || Features.ExpeditionEvent.Isolates(Owner) || (uint)_ID == 0 || (uint)_ID > int.MaxValue
                        || deposit && Features.Housing.Enabled && _ID==WarehouseID.House
                        || !Owner.Warehouses.TryGetValue(_ID, out registered) || !ReferenceEquals(registered, this)) return false;
                    ConquerItem item;
                    if (deposit)
                    {
                        if (Count >= MaxCount || !Owner.Inventory.TryGetItem(uid, out item)
                            || items.ContainsKey(uid) || item.Warehouse != 0 || item.Position != 0 || item.InWardrobe
                            || Features.MoonBoxQuest.Material(item.ID) || ConquerItem.isRune(item.ID)
                            || item.ID >= 729960 && item.ID <= 729970) return false;
                    }
                    else
                    {
                        if (Owner.Inventory.Count >= 40 || Owner.Inventory.ContainsUID(uid)
                            || !items.TryGetValue(uid, out item) || item.Warehouse != (uint)_ID || item.Position != 0) return false;
                    }
                    if (!Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID)) return false;
                    uint from = deposit ? 0u : (uint)_ID;
                    uint to = deposit ? (uint)_ID : 0u;
                    using (var c = Database.DataHolder.MySqlConnection)
                    {
                        c.Open();
                        using (var t = c.BeginTransaction())
                        {
                            Database.SkillTable.LockOwner(c, t, Owner);
                            if(HouseBox && !Features.HouseFurniture.StorageAccess(c,t,Owner,(uint)_ID))return false;
                            using (var q = Features.QuestInventory.Command(c, t,
                                "SELECT count(*) FROM items WHERE entityid=@owner AND warehouse=@dest AND position=0 AND COALESCE(inwardrobe,0)=0",
                                "owner", (long)Owner.Entity.UID, "dest", (int)to))
                                if (Convert.ToInt64(q.ExecuteScalar()) >= (deposit ? MaxCount : 40)) return false;
                            if (Features.QuestInventory.Execute(c, t, @"UPDATE items SET warehouse=@dest,position=0
WHERE uid=@item AND entityid=@owner AND id=@type AND warehouse=@source AND position=0
AND COALESCE(stacksize,0)=@stack AND COALESCE(bound,0)=@bound AND COALESCE(inwardrobe,0)=0",
                                "dest", (int)to, "item", (long)uid, "owner", (long)Owner.Entity.UID,
                                "type", (int)item.ID, "source", (int)from, "stack", (int)item.StackSize, "bound", item.Bound ? 1 : 0) != 1)
                                throw new InvalidOperationException("Stored item ownership changed.");
                            if (!Features.QuestInventory.Player(Owner)) throw new InvalidOperationException("Storage session changed.");
                            t.Commit();
                        }
                    }
                    // No runtime mutation, notification, stack merge or second database write before commit.
                    item.Warehouse = to;
                    if (deposit)
                    {
                        if (!Owner.Inventory.Remove(uid, Enums.ItemUse.None, true))
                            throw new InvalidOperationException("Committed storage deposit requires reload.");
                        items.Add(uid, item);
                    }
                    else
                    {
                        item.StatsLoaded = false;
                        if (!Owner.Inventory.Add(item, Enums.ItemUse.None) || !Owner.Inventory.ContainsUID(uid))
                            throw new InvalidOperationException("Committed storage withdrawal requires reload.");
                        items.Remove(uid);
                        item.Send(Owner);
                    }
                    objects = items.Values.ToArray();
                    return true;
                }
                catch (Exception ex)
                {
                    Features.QuestInventory.Fail(Owner, "warehouse-transfer", ex);
                    return false;
                }
            }
        }
        public enum WarehouseID : uint
        {
            TwinCity = 8,
            PhoenixCity = 10012,
            ApeCity = 10028,
            DesertCity = 10011,
            BirdCity = 10027,
            StoneCity = 4101,
            Market = 44,
            House = 100
        }
    }
}
