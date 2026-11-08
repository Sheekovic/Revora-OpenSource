using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Revora.Systems_OverPower.WardRobeSystem;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Network.GamePackets
{
    public sealed class CoatStorage
    {
        public CoatStorage() { }
        private static int diagnosticRequests;
        private static void TraceRequest(Client.GameState client, CoatStorageProto request)
        {
            if (global::System.Threading.Interlocked.Increment(ref diagnosticRequests) > 100) return;
            ConquerItem item;
            bool stored = client.Entity.StorageItems.TryGetValue(request.ItemGuid, out item);
            if (!stored) client.Inventory.TryGetItem(request.ItemGuid, out item);
            Console.WriteLine("[wardrobe] action=" + (int)request.ActionId + " owner=" + client.Entity.UID
                + " item=" + request.ItemGuid + " requested-type=" + request.ItemId
                + " player-ready=" + Q.Player(client) + " session-action=" + client.Action
                + " trade=" + (client.Trade == null || client.Trade.InTrade) + " booth=" + (client.Booth != null)
                + " stored=" + stored + " found=" + (item != null)
                + (item == null ? "" : " type=" + item.ID + " slot=" + item.Position + " stack=" + item.StackSize
                    + " wardrobe=" + item.InWardrobe + " warehouse=" + item.Warehouse));
        }
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
            lock(client.ItemSyncRoot) {
                TraceRequest(client, Info);
                HandleCore(client);
                if(TitleRequirements.Ready(client))TitleRequirements.Reconcile(client,false);
            }
        }
        private void HandleCore(Client.GameState client)
        {
            switch (Info.ActionId)
            {
                case Action.Equipcheck:

                    if (Illegal(Info, client))
                        return;

                    ConquerItem myItem;
                   
                    if ((client.Inventory.TryGetItem(Info.ItemGuid, out myItem)
                        || client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                        && CanPrepareWear(client, myItem))
                    {
                        var packet = FinalizeProtoBuf(new CoatStorageProto()
                        {
                            ActionId = Info.ActionId,
                            ItemGuid = Info.ItemGuid,
                        });
                        client.Send(packet);
                    }
                    else 
                    {
                        client.MessageBox("You cannot wear it. Your inventory is full!");
                    }
                    break;
                case Action.Combine:
                   {
                       ConquerItem ItemInInventory;
                       ConquerItem ItemInSystem;
                       if (client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out ItemInSystem))
                       {
                           if (client.Inventory.TryGetItem(Info.ItemGuid, out ItemInInventory))
                           {
                               
                               client.Disconnect();
                           }
                           //else 
                           //{
                           //    client.Inventory.Remove(ItemInInventory.UID, Game.Enums.ItemUse.Remove, true);
                           //}
                       }
                       break;
                    }
                //case Action.Addtowardrobe:
                //    {
                //        if (Illegal(Info, client))
                //            return;
                //        if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
                //            client.Player.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                //        {
                //            if (!client.Player.StorageItems.ContainsKey(myItem.UID))
                //            {
                //                client.Player.StorageItems.Add(myItem.UID, myItem);
                //                myItem.InWardrobe = true;
                //                Database.ConquerItemTable.UpdateWardrobe(myItem.InWardrobe, myItem.UID);
                //                client.Inventory.Remove(myItem, Game.Enums.ItemUse.None);
                //            }
                //            var dbInfo = Database.ConquerItemInformation.BaseInformations[myItem.ID];
                //            var pos = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                //            if (pos == 17)
                //            {
                //                if (client.Player.SubClasses.Classes.ContainsKey(9))
                //                    if (client.Player.SubClasses.Classes[9].Phase >= dbInfo.Proficiency)
                //                    {
                //                        client.Send(FinalizeProtoBuf(new CoatStorageProto()
                //                        {
                //                            ActionId = Info.ActionId,
                //                            ItemGuid = Info.ItemGuid,
                //                            ItemId = (int)myItem.ID
                //                        }));
                //                        foreach (var i in client.Player.StorageItems.Values)
                //                            if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                //                            {
                //                                i.Position = 0;
                //                                Database.ConquerItemTable.UpdateLocation(i, client);
                //                            }
                //                        myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);

                //                        if (!client.Equipment.Add(myItem))
                //                        {
                //                            if (client.Inventory.Count <= 39 )
                //                            {

                //                            client.Equipment.Remove((byte)myItem.Position, true);
                //                            if (!client.Equipment.Add(myItem))
                //                                client.MessageBox("Sorry!");
                //                            }
                //                            else
                //                            {
                //                                client.Disconnect();
                //                            }
                //                            #region Combine Garment
                //                            ConquerItem ItemInInventory;
                //                            ConquerItem ItemInSystem;
                //                            if (client.Player.StorageItems.TryGetValue(Info.ItemGuid, out ItemInSystem))
                //                            {
                //                                if (client.Inventory.TryGetItem(Info.ItemGuid, out ItemInInventory)) // && ItemInSystem.Bless <= 1)
                //                                {
                //                                    //client.Inventory.Remove(ItemInInventory.UID, Game.Enums.ItemUse.Remove, true);
                //                                    client.Disconnect();
                //                                }
                                                
                //                            }
                //                            break;

                //                            #endregion
                //                        }
                //                        var iu = new ItemUsage(true);
                //                        iu.ID = (uint)ItemUsage.Mode.Unknown5;
                //                        iu.UID = myItem.UID;
                //                        iu.dwParam = myItem.Position;
                //                        client.Send(iu.ToArray());

                //                        ClientEquip equips = new ClientEquip();
                //                        equips.DoEquips(client);
                //                        client.Send(equips.ToArray());

                //                        Database.ConquerItemTable.UpdateLocation(myItem, client);
                //                        client.Equipment.UpdateEntityPacket();
                //                    }
                //                    else
                //                        break;
                //                if (Database.StorageItem.AmountStarMounts(client, 5) >= 1)
                //                {
                //                    //new TitleStorage().AddTitle(client, 6004, 23, false);
                //                }
                //                if (Database.StorageItem.AmountStarMounts(client, 4) >= 5)
                //                {
                //                    //new TitleStorage().AddTitle(client, 2006, 7, false);
                //                }
                //            }
                //            if (pos == 9)
                //            {
                //                client.Send(FinalizeProtoBuf(new CoatStorageProto()
                //                {
                //                    ActionId = Info.ActionId,
                //                    ItemGuid = Info.ItemGuid,
                //                    ItemId = (int)myItem.ID
                //                }));
                //                foreach (var i in client.Player.StorageItems.Values)
                //                    if (i.Position != 0 && i.Position == (byte)ItemHandler.GetPositionFromID(myItem.ID))
                //                    {
                //                        i.Position = 0;
                //                        Database.ConquerItemTable.UpdateLocation(i, client);
                //                    }
                //                myItem.Position = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                //                if (!client.Equipment.Add(myItem))
                //                {
                //                    if (client.Inventory.Count <= 39)
                //                    {
                //                        client.Equipment.Remove((byte)myItem.Position, true);
                //                        if (!client.Equipment.Add(myItem))
                //                            throw new Exception();
                //                    }
                //                    else
                //                    {
                //                        client.MessageBox("Your inventory is full!");
                //                        client.Disconnect();
                //                    }
                //                }
                //                var iu = new ItemUsage(true);
                //                iu.ID = (uint)ItemUsage.Mode.Unknown5;
                //                iu.UID = myItem.UID;
                //                iu.dwParam = myItem.Position;
                //                client.Send(iu.ToArray());

                //                ClientEquip equips = new ClientEquip();
                //                equips.DoEquips(client);

                //                if ((equips.Garment == 0 || equips.Garment > 0 && equips.Garment != myItem.UID) && myItem.Position != 17)
                //                    equips.Garment = myItem.UID;

                //                client.Send(equips.ToArray());

                //                Database.ConquerItemTable.UpdateLocation(myItem, client);
                //                client.Equipment.UpdateEntityPacket();
                //                if (Database.StorageItem.AmountStarGarments(client, 5) >= 1)
                //                {
                //                    //new TitleStorage().AddTitle(client, 6003, 22, false);
                //                }
                //                if (Database.StorageItem.AmountStarGarments(client, 4) >= 5)
                //                {
                //                    //new TitleStorage().AddTitle(client, 2005, 6, false);
                //                }
                //            }
                //            break;
                //        }
                //        if (Database.StorageItem.AmountStarGarments(client, 5) >= 1)
                //        {
                //            //new TitleStorage().AddTitle(client, 6003, 22, false);
                //        }
                //        if (Database.StorageItem.AmountStarMounts(client, 5) >= 1)
                //        {
                //            //new TitleStorage().AddTitle(client, 6004, 23, false);
                //        }
                //        if (Database.StorageItem.AmountStarGarments(client, 4) >= 5)
                //        {
                //            //new TitleStorage().AddTitle(client, 2005, 6, false);
                //        }
                //        if (Database.StorageItem.AmountStarMounts(client, 4) >= 5)
                //        {
                //            //new TitleStorage().AddTitle(client, 2006, 7, false);
                //        }
                //        break;
                //    }
                #region New AddToWardRobe (do not remove)
                case Action.Addtowardrobe:
                   {
                       if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null)
                           return;
                       if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
                           client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out myItem))
                       {
                           var equippedSlot = (byte)ItemHandler.GetPositionFromID(myItem.ID);
                           if (myItem.UID != Info.ItemGuid || !MatchesWearSelector(myItem, Info.ItemId)
                               || !Database.ConquerItemInformation.BaseInformations.ContainsKey(myItem.ID)
                               || (equippedSlot != ConquerItem.Garment && equippedSlot != ConquerItem.SteedArmor))
                               return;
                           if ((equippedSlot == ConquerItem.Garment || equippedSlot == ConquerItem.SteedArmor)
                               && myItem.InWardrobe && myItem.Position == equippedSlot
                               && ReferenceEquals(client.Equipment.TryGetItem(equippedSlot), myItem))
                           {
                               client.Send(FinalizeProtoBuf(new CoatStorageProto { ActionId = Info.ActionId,
                                   ItemGuid = myItem.UID, ItemId = (int)myItem.ID }));
                               break;
                           }
                           if (!client.Entity.StorageItems.ContainsKey(myItem.UID))
                           {
                               if (!Deposit(client, myItem)) return;
                           }
                           if (EquipStored(client, myItem))
                               client.Send(FinalizeProtoBuf(new CoatStorageProto { ActionId = Info.ActionId,
                                   ItemGuid = myItem.UID, ItemId = (int)myItem.ID }));
                           break;
                       }

                       break;
                   }
                #endregion
                case Action.Takeoff:
                    if (client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out myItem)
                        && TakeOff(client, myItem, Info.ItemId))
                        client.Send(FinalizeProtoBuf(new CoatStorageProto {
                            ActionId = Info.ActionId, ItemGuid = Info.ItemGuid, ItemId = (int)myItem.ID }));
                    break;
                case Action.Retrieve:
                    if (client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out myItem)
                        && Withdraw(client, Info.ItemGuid, Info.ItemId))
                        client.Send(FinalizeProtoBuf(new CoatStorageProto {
                            ActionId = Info.ActionId, ItemGuid = Info.ItemGuid, ItemId = (int)myItem.ID }));
                    break;
            }
        }
        private static bool MatchesWearSelector(ConquerItem item, int selector)
        {
            int slot = (int)ItemHandler.GetPositionFromID(item.ID);
            return (slot == ConquerItem.Garment || slot == ConquerItem.SteedArmor)
                && (selector == 0 || selector == (int)item.ID || selector == slot);
        }

        private static bool CanPrepareWear(Client.GameState client, ConquerItem item)
        {
            if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null
                || item.Warehouse != 0 || item.StackSize > 1
                || (item.Days > 0 && item.DayStamp.AddDays(item.Days) <= DateTime.Now)) return false;
            byte slot = (byte)ItemHandler.GetPositionFromID(item.ID);
            if (slot != ConquerItem.Garment && slot != ConquerItem.SteedArmor) return false;
            var previous = client.Equipment.TryGetItem(slot);
            // Replacing a stored cosmetic does not consume a bag slot. Depositing
            // the selected bag item also frees a slot for a displaced bag cosmetic.
            return previous == null || previous.InWardrobe || previous.UID == item.UID
                || !item.InWardrobe || client.Inventory.Count < 40;
        }

        // Legacy wardrobe equipment uses zero for a single item. Accept 0/1,
        // reject actual stacks, and compare the exact saved stack in each write.
        private static bool EquipStored(Client.GameState client, ConquerItem item)
        {
            if (!Q.Player(client) || !item.InWardrobe || item.Position != 0 || item.Warehouse != 0
                || item.StackSize > 1 || client.Trade == null || client.Trade.InTrade || client.Booth != null
                || (item.Days > 0 && item.DayStamp.AddDays(item.Days) <= DateTime.Now)) return false;
            byte slot = (byte)ItemHandler.GetPositionFromID(item.ID);
            if (slot != ConquerItem.Garment && slot != ConquerItem.SteedArmor) return false;
            if (slot == ConquerItem.Garment && client.Entity.MapID == 10360) return false;
            if (slot == ConquerItem.SteedArmor && (client.Entity.SubClasses == null
                || !client.Entity.SubClasses.Classes.ContainsKey(9)
                || client.Entity.SubClasses.Classes[9].Phase < Database.ConquerItemInformation.BaseInformations[item.ID].Proficiency)) return false;
            var previous = client.Equipment.TryGetItem(slot);
            if (previous != null && (previous.Position != slot || previous.Warehouse != 0
                || previous.StackSize > 1 || (!previous.InWardrobe && client.Inventory.Count >= 40))) return false;
            try {
                using (var c = Database.DataHolder.MySqlConnection) {
                    c.Open();
                    using (var t = c.BeginTransaction()) {
                        Database.SkillTable.LockOwner(c, t, client);
                        if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null) return false;
                        var savedSlot = new HashSet<uint>();
                        using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@owner AND position=@slot AND warehouse=0 FOR UPDATE",
                            "owner", (long)client.Entity.UID, "slot", (int)slot))
                        using (var r = q.ExecuteReader()) while (r.Read()) savedSlot.Add(checked((uint)r.GetInt64(0)));
                        if (!savedSlot.SetEquals(previous == null ? new uint[0] : new uint[]{previous.UID}))
                            throw new InvalidOperationException("Wardrobe equipment slot requires reload.");
                        if (previous != null) {
                            if (!previous.InWardrobe) {
                                var savedBag = new HashSet<uint>();
                                using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE entityid=@owner
AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "owner", (long)client.Entity.UID))
                                using (var r = q.ExecuteReader()) while (r.Read()) savedBag.Add(checked((uint)r.GetInt64(0)));
                                if (savedBag.Count >= 40 || !savedBag.SetEquals(client.Inventory.Objects.Select(i => i.UID)))
                                    throw new InvalidOperationException("Displaced cosmetic bag requires reload.");
                            }
                            if (Q.Execute(c, t, @"UPDATE items SET position=0 WHERE uid=@item AND entityid=@owner
AND id=@type AND position=@slot AND warehouse=0 AND stacksize=@previousStack AND COALESCE(inwardrobe,0)=@wardrobe",
                                "item", (long)previous.UID, "owner", (long)client.Entity.UID, "type", (int)previous.ID,
                                "slot", (int)slot, "previousStack", (int)previous.StackSize, "wardrobe", previous.InWardrobe ? 1 : 0) != 1)
                                throw new InvalidOperationException("Displaced cosmetic write was suppressed.");
                        }
                        if (Q.Execute(c, t, @"UPDATE items SET position=@slot WHERE uid=@item AND entityid=@owner
AND id=@type AND position=0 AND warehouse=0 AND inwardrobe=1 AND stacksize=@stack AND bound=@bound
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "slot", (int)slot, "item", (long)item.UID, "owner", (long)client.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                            "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Selected cosmetic write was suppressed.");
                        t.Commit();
                    }
                }
                if (previous != null) {
                    client.Equipment.UnequipCommittedCosmetic(slot, previous);
                    if (!previous.InWardrobe) {
                        if (!client.Inventory.Add(previous, Game.Enums.ItemUse.None))
                            throw new InvalidOperationException("Committed displaced cosmetic requires reload.");
                        previous.Send(client);
                    }
                }
                item.Position = slot;
                if (!client.Equipment.Add(item)) throw new InvalidOperationException("Committed cosmetic equip requires reload.");
                var update = new ItemUsage(true); update.ID = (uint)ItemUsage.Mode.Unknown5;
                update.UID = item.UID; update.dwParam = slot; client.Send(update.ToArray());
                var equips = new ClientEquip(); equips.DoEquips(client); client.Send(equips.ToArray());
                client.Equipment.UpdateEntityPacket();
                return true;
            } catch (Exception ex) { Q.Fail(client, "wardrobe-equip", ex); return false; }
        }

        private static bool TakeOff(Client.GameState client, ConquerItem item, int requestedType)
        {
            if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null
                || !item.InWardrobe || item.Warehouse != 0 || item.StackSize > 1
                || (requestedType != 0 && requestedType != (int)item.ID)
                || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID)) return false;
            byte slot = (byte)ItemHandler.GetPositionFromID(item.ID);
            if ((slot != ConquerItem.Garment && slot != ConquerItem.SteedArmor)
                || item.Position != slot || !ReferenceEquals(client.Equipment.TryGetItem(slot), item)) return false;
            if (slot == ConquerItem.Garment && client.Entity.MapID == 10360) return false;
            try {
                using (var c = Database.DataHolder.MySqlConnection) {
                    c.Open();
                    using (var t = c.BeginTransaction()) {
                        Database.SkillTable.LockOwner(c, t, client);
                        if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null)
                            return false;
                        if (Q.Execute(c, t, @"UPDATE items SET position=0
WHERE uid=@item AND entityid=@owner AND id=@type AND position=@slot AND warehouse=0
AND inwardrobe=1 AND stacksize=@stack", "item", (long)item.UID, "owner", (long)client.Entity.UID,
                            "type", (int)item.ID, "stack", (int)item.StackSize, "slot", (int)slot) != 1)
                            throw new InvalidOperationException("Wardrobe takeoff ownership changed or write was suppressed.");
                        t.Commit();
                    }
                }
                client.Equipment.UnequipCommittedCosmetic(slot, item);
                client.LoadItemStats();
                var equips = new ClientEquip(); equips.DoEquips(client); client.Send(equips.ToArray());
                client.Equipment.UpdateEntityPacket();
                return true;
            } catch (Exception ex) { Q.Fail(client, "wardrobe-takeoff", ex); return false; }
        }

        private static bool Withdraw(Client.GameState client, uint uid, int requestedType)
        {
            ConquerItem item;
            if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null
                || !client.Entity.StorageItems.TryGetValue(uid, out item) || item.UID != uid
                || !item.InWardrobe || client.Inventory.ContainsUID(uid) || client.Inventory.Count >= 40
                || (requestedType != 0 && requestedType != (int)item.ID)
                || !Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID)) return false;
            byte slot = (byte)ItemHandler.GetPositionFromID(item.ID);
            if ((slot != ConquerItem.Garment && slot != ConquerItem.SteedArmor) || item.Warehouse != 0
                || item.StackSize > 1 || (item.Position != 0 && item.Position != slot)) return false;
            bool equipped = item.Position == slot;
            if (equipped != ReferenceEquals(client.Equipment.TryGetItem(slot), item)) return false;
            try {
                using (var c = Database.DataHolder.MySqlConnection) {
                    c.Open();
                    using (var t = c.BeginTransaction()) {
                        Database.SkillTable.LockOwner(c, t, client);
                        if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null)
                            return false;
                        var savedBag = new HashSet<uint>();
                        using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE entityid=@owner
AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "owner", (long)client.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read()) savedBag.Add(checked((uint)r.GetInt64(0)));
                        if (savedBag.Count >= 40 || !savedBag.SetEquals(client.Inventory.Objects.Select(i => i.UID)))
                            throw new InvalidOperationException("Wardrobe withdrawal bag requires reload.");
                        if (Q.Execute(c, t, @"UPDATE items SET inwardrobe=0,position=0
WHERE uid=@item AND entityid=@owner AND id=@type AND position=@position AND warehouse=0
AND stacksize=@stack AND inwardrobe=1 AND bound=@bound
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item", (long)item.UID, "owner", (long)client.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                            "position", (int)item.Position, "bound", item.Bound ? 1 : 0,
                            "days", item.Days.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Wardrobe withdrawal ownership changed or write was suppressed.");
                        t.Commit();
                    }
                }
                if (equipped) client.Equipment.UnequipCommittedCosmetic(slot, item);
                item.Position = 0; item.IsWorn = false; item.InWardrobe = false; item.StatsLoaded = false;
                client.Entity.StorageItems.Remove(uid);
                if (!client.Inventory.Add(item, Game.Enums.ItemUse.None))
                    throw new InvalidOperationException("Committed wardrobe withdrawal requires reload.");
                item.Send(client);
                if (equipped) client.LoadItemStats();
                client.Equipment.UpdateEntityPacket();
                return true;
            } catch (Exception ex) {
                Q.Fail(client, "wardrobe-withdrawal", ex);
                return false;
            }
        }

        private static bool Deposit(Client.GameState client, ConquerItem item)
        {
            if (item.Position != 0 || item.Warehouse != 0 || item.InWardrobe || item.StackSize > 1
                || (item.Days > 0 && item.DayStamp.AddDays(item.Days) <= DateTime.Now)) return false;
            try {
                using (var c = Database.DataHolder.MySqlConnection) {
                    c.Open();
                    using (var t = c.BeginTransaction()) {
                        Database.SkillTable.LockOwner(c, t, client);
                        if (!Q.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null)
                            return false;
                        if (Q.Execute(c, t, @"UPDATE items SET inwardrobe=1
WHERE uid=@item AND entityid=@owner AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND COALESCE(inwardrobe,0)=0 AND bound=@bound
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item", (long)item.UID, "owner", (long)client.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                            "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Wardrobe deposit ownership changed or write was suppressed.");
                        t.Commit();
                    }
                }
                item.InWardrobe = true;
                if (!client.Inventory.Remove(item, Game.Enums.ItemUse.None))
                    throw new InvalidOperationException("Committed wardrobe deposit requires reload.");
                client.Entity.StorageItems.Add(item.UID, item);
                if (client.Entity.MyAchievement != null) client.Entity.MyAchievement.Invalidate();
                return true;
            } catch (Exception ex) {
                Q.Fail(client, "wardrobe-deposit", ex);
                return false;
            }
        }

        private bool Illegal(CoatStorageProto Info, Client.GameState client)
        {
            ConquerItem myItem;
            if (client.Inventory.TryGetItem(Info.ItemGuid, out myItem) ||
            client.Entity.StorageItems.TryGetValue(Info.ItemGuid, out myItem) ||
            client.Equipment.TryGetItem(Info.ItemGuid, out myItem))
            {
                var dbInfo = Database.ConquerItemInformation.BaseInformations.ContainsKey(myItem.ID) ?
                Database.ConquerItemInformation.BaseInformations[myItem.ID] : null;
                if (dbInfo == null)
                    return true;
                var charSex = (client.Entity.Body == 1006 || client.Entity.Body == 2005) ? "Male" : "Female";
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
            if (client == null || client.Entity == null || client.Entity.StorageItems == null) return;
            var pkt = new CoatStorageProto();
            foreach (var item in client.Entity.StorageItems.Values)
            {
                pkt.AddItem(item,
                    client.Entity.StorageItems.Values.Where(i => i.ID == item.ID).Count());
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
                    if (currentGarment != null && !client.Entity.StorageItems.ContainsKey(currentGarment.UID))
                    {
                        client.Entity.StorageItems.Add(currentGarment.UID, currentGarment);
                        pkt.AddItem(currentGarment,
                            client.Entity.StorageItems.Values.Where(i => i.ID == currentGarment.ID).Count());
                        pkt.Item.Equipped = true;
                        currentGarment.InWardrobe = true;
                        client.Send(FinalizeProtoBuf(pkt));
                        Database.ConquerItemTable.UpdateWardrobe(currentGarment.InWardrobe, currentGarment.UID);
                    }
                    var currentMountArmor = client.Equipment.TryGetItem((byte)ItemHandler.Positions.SteedArmor);
                    if (currentMountArmor != null && !client.Entity.StorageItems.ContainsKey(currentMountArmor.UID))
                    {
                        client.Entity.StorageItems.Add(currentMountArmor.UID, currentMountArmor);
                        pkt.AddItem(currentMountArmor,
                            client.Entity.StorageItems.Values.Where(i => i.ID == currentMountArmor.ID).Count());
                        pkt.Item.Equipped = true;
                        currentMountArmor.InWardrobe = true;
                        client.Send(FinalizeProtoBuf(pkt));
                        Database.ConquerItemTable.UpdateWardrobe(currentMountArmor.InWardrobe, currentMountArmor.UID);

                    }
                    var garments = new int[] { 193445, 193525, 188915, 189065 };
                    var mounts = new int[] { 200449, 200517, 200531, 200540, 200482, 200482 };
                    foreach (var i in client.Entity.StorageItems.Values)
                    {
                        foreach (var garment in garments)
                            if (i.ID == garment)
                            {
                                //new TitleStorage().AddTitle(client, 2005, 6, false);
                                //new TitleStorage().AddTitle(client, 6003, 22, false);
                            }
                        foreach (var mount in mounts)
                            if (i.ID == mount)
                            {
                                //new TitleStorage().AddTitle(client, 2006, 7, false);
                                //new TitleStorage().AddTitle(client, 6004, 23, false);
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
        public Revora.Game.Enums.ItemEffect Effect;
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
        public Revora.Game.Enums.Color Color;
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
