#region Old Usage

using Revora.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revora.Network.GamePackets
{
    public unsafe class ItemUsage : Writer, Interfaces.IPacket
    {
        public const ushort
            DragonBallUpgrade = 19,
            Unknown5 = 5,
            MeteorUpgrade = 20;
        public enum Mode : byte
        {
            BuyFromNPC = 1,
            SellToNPC = 2,
            RemoveInventory = 3,
            EquipItem = 4,
            Unknown5 = 5,
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
        public uint dwParam2
        {
            get { return BitConverter.ToUInt32(Packet, 91); }
            set { Write(value, 91, Packet); }
        }
        public ulong dwParamMoney
        {
            get { return BitConverter.ToUInt32(Packet, 12); }
            set { WriteUInt64(value, 12, Packet); }
        }
        public ulong Money
        {// this only for Bank !
            get { return BitConverter.ToUInt64(Packet, 12); }
            set { Writer.WriteUInt64(value, 12, Packet); }
        }
        public ulong BoothCost
        {
            get { return BitConverter.ToUInt64(Packet, 12); }
            set { WriteUInt64(value, 12, Packet); }
        }
        public Mode Action
        {
            get
            {
                byte at20 = Packet[20];
                if (at20 == (byte)Mode.MainEquipment || at20 == (byte)Mode.AlternateEquipment)
                    return (Mode)at20;
                if (Packet.Length > 16)
                {
                    byte at16 = Packet[16];
                    if (at16 == (byte)Mode.MainEquipment || at16 == (byte)Mode.AlternateEquipment)
                        return (Mode)at16;
                }
                return (Mode)at20;
            }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 20)) = (byte)value; }
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Packet, 20); }
            set { WriteUInt32(value, 20, Packet); }
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
            // Read no action bytes before its complete fixed prefix is present.
            // Individual services validate the remaining fields they consume.
            if (client == null || client.Entity == null || client.Action != 2 || packet == null || packet.Length < 22) return;
            ItemUsage usage = new ItemUsage(false);
            usage.Deserialize(packet);
            ushort rawAction = BitConverter.ToUInt16(packet, 20);
            if (rawAction == 1 && usage.UID == Database.ChampionShop.UID)
            {
                Revora.Game.Features.ChampionPurchases.Buy(client, usage);
                return;
            }
            if (rawAction == 39 || rawAction == 51) { Revora.Game.Features.GemComposition.Handle(client, packet); return; }
            if ((rawAction == 1 || rawAction == 55) && Revora.Game.Features.TravelScrollShop.TryBuy(client, usage)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ChampionSteedPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.MysteryFruit.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.MeditationPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ExperiencePotions.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.TournamentExpPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ExperienceProtection.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ExpCareCombine.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.TimeDropHourglasses.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.WelcomePacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.StarterPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.RevoraStarterPack.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.P7SoulChoicePack.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.RebirthGarmentPack.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ConquerPointBags.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.FixedOutputPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.DragonBallPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PrizeCenterPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.EasterGoldPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.DailySignInPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.AncientStoneCombine.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.StarStoneCombine.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.AttackDefensePotions.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PowerArenaPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.HealingMedicine.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.BloodshedSeaBoosts.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.HonorRefineryPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.GainRefineryPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.HossuHairPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.RedRunePacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.YellowBlueRunePacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PlusOneStonePacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.HigherStonePacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.GemDragonSoulPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ChiPointItems.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.SilverStudyBags.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.SilverPouches.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.StudyBooks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.ChiTokenCash.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.QuestGiftPacks.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PenitenceAmulets.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.MonkSutta.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.TaoistEpicQuest.TryHandle(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PirateEpicWorld.TryHandle(client, packet)) return;
            if ((usage.Action == Mode.MergeStackableItems || usage.Action == Mode.SplitStack) && Revora.Game.Features.PirateEpicWorld.TryStack(client, packet)) return;
            if (usage.Action == Mode.EquipItem && Revora.Game.Features.PrayingStones.TryHandle(client, packet)) return;
            if ((rawAction == 1 || rawAction == 55) && Revora.Game.Features.ExemptionTokenShop.TryBuy(client, usage)) return;
            if ((rawAction == 1 || rawAction == 55) && Revora.Game.Features.WeaponBookShop.TryBuy(client, usage)) return;
            if (!client.Entity.Dead || usage.Action == ItemUsage.Mode.Ping)
            {
                switch (usage.Action)
                {
                    #region HandleRunes
                    case (ItemUsage.Mode)57:
                    case (ItemUsage.Mode)58:
                    case (ItemUsage.Mode)59:
                    case (ItemUsage.Mode)60:
                    case (ItemUsage.Mode)61:
                    case (ItemUsage.Mode)62:
                    case (ItemUsage.Mode)63:
                    case (ItemUsage.Mode)64:
                    case (ItemUsage.Mode)65:
                    case (ItemUsage.Mode)66:
                        {
                            RunesSystem.HandlePacket1009(client, packet);
                            break;
                        }
                    //case (ItemUsage.Mode)67://decompse relic
                    //    {
                    //        int relicsSoulCount = 0;
                    //        for (int i = 0; i < usage.dwExtraInfo; i++)
                    //        {
                    //            uint itemuid = BitConverter.ToUInt32(packet, 91 + (i * 4));
                    //            ConquerItem item;
                    //            if (client.Inventory.TryGetItem(itemuid, out item))
                    //            {
                    //                if (item.ID == 4100001 || item.ID == 4100002 || item.ID == 4100003 || item.ID == 4100004 || item.ID == 4100005)
                    //                {
                    //                    relicsSoulCount += Relics.GetSoulCount(item.RelicAttrib1);
                    //                    relicsSoulCount += Relics.GetSoulCount(item.RelicAttrib2);
                    //                    relicsSoulCount += Relics.GetSoulCount(item.RelicAttrib3);
                    //                    relicsSoulCount += Relics.GetSoulCount(item.RelicAttrib4);
                    //                    relicsSoulCount += Relics.GetSoulCount(item.RelicAttrib5);
                    //                    client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                    //                }
                    //            }
                    //        }
                    //        client.Inventory.Add(3306885, 0, (byte)relicsSoulCount);
                    //        usage.Send(client);
                    //        break;
                    //    }
                    #endregion
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
                            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow))
                            {
                                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PathOfShadow);
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
                                if (client.Player.ExtraInventory < 200)
                                {
                                    if (Sash.ID == 1100003)
                                    {
                                        if (client.Player.ExtraInventory < 198)
                                        {
                                            client.Player.ExtraInventory += 3;
                                        }
                                        else
                                        {
                                            client.Player.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                    else if (Sash.ID == 1100006)
                                    {
                                        if (client.Player.ExtraInventory < 195)
                                        {
                                            client.Player.ExtraInventory += 6;
                                        }
                                        else
                                        {
                                            client.Player.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                    else if (Sash.ID == 1100009)
                                    {
                                        if (client.Player.ExtraInventory < 189)
                                        {
                                            client.Player.ExtraInventory += 12;
                                        }
                                        else
                                        {
                                            client.Player.ExtraInventory = 200;
                                        }
                                        client.Inventory.Remove(Sash, Game.Enums.ItemUse.Remove);
                                    }
                                }
                                else
                                {
                                    client.Player.SendSysMesage("You cannot open more slots.");
                                }
                            }
                            break;
                        }*/
                    case ItemUsage.Mode.ItemsReturned:
                        {
                            Revora.Game.Features.MailRewards.ReturnOwnedParcel(client, usage.UID);
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
                        { // dwParam JHEISSON
                            usage.dwParamMoney = (ulong)client.MoneySave;
                            client.Send(usage);
                            break;
                        }
                    case ItemUsage.Mode.WarehouseDeposit:
                        {
                            if ((ulong)client.Entity.Money >= (ulong)usage.Money)
                            {
                                client.Entity.AdjustMoney(-(decimal)((ulong)usage.Money));
                                client.MoneySave += (ulong)usage.Money;
                            }
                            break;
                        }
                    case ItemUsage.Mode.WarehouseWithdraw:
                        {
                            if ((ulong)client.MoneySave >= (ulong)usage.Money)
                            {
                                client.Entity.AdjustMoney((decimal)((ulong)usage.Money));
                                client.MoneySave -= (ulong)usage.Money;
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
                                    Message message = new Message(" " + client.Entity.Name + " has redeemed your gear and pay " + item.ConquerPointsCost + " CPS Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
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
                                        client.Send(new Message("Item cannot be claimed!", System.Drawing.Color.Red, Message.TopLeft));
                                        return;
                                    }
                                    if (DateTime.Now < item.Date.AddDays(7) && item.OwnerUID != 500)
                                    {
                                        client.Send(new Message("This item has not expired. You can not claim yet!", System.Drawing.Color.Red, Message.TopLeft));
                                        return;
                                    }
                                    if (item.OwnerUID == 500)
                                        client.Entity.AddConquerPoints(item.ConquerPointsCost);
                                    else
                                    {
                                        client.Inventory.Add(item.Item, Game.Enums.ItemUse.Move);
                                        Message message = new Message("Thanks for arresting the player " + item.OwnerName + " , " + item.GainerName + ". The arrested person has redeemed his articles and received a large amount of Cps as a reward. Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
                                        Kernel.SendWorldMessage(message, Server.GamePool);
                                    }
                                    Database.ClaimItemTable.Claim(item, client);
                                    client.ClaimableItem.Remove(item.UID);
                                    usage.dwParam = client.Entity.UID;
                                    usage.dwExtraInfo3 = item.ConquerPointsCost;
                                    client.Send(usage);
                                    Message message2 = new Message("Thanks for arresting the player " + client.Entity.Name + " You've received " + item.ConquerPointsCost + " CPS Congratulations!", System.Drawing.Color.Wheat, Message.Talk);
                                    Kernel.SendWorldMessage(message2, Server.GamePool);
                                }
                                else client.Send(new Message("The object you wish to claim has already been claimed.", System.Drawing.Color.Red, Message.TopLeft));
                            }
                            break;
                        }
                    case ItemUsage.Mode.SocketerMan:
                        Revora.Game.Features.EquipmentSockets.Handle(client,packet);
                        break;
                    case ItemUsage.Mode.Bless:
                        Revora.Game.Features.EquipmentUpgrades.Bless(client, packet);
                        break;
                    case ItemUsage.Mode.SplitStack:
                        {
                            ConquerItem mainItem = null;
                            ConquerItem minorItem = new ConquerItem(true);
                            Database.ConquerItemInformation infos = null;
                            minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                            if (client.Inventory.TryGetItem(usage.UID, out mainItem))
                            {
                                infos = new Database.ConquerItemInformation(mainItem.ID, 0);
                                if (usage.dwParam > 0 && usage.dwParam < mainItem.StackSize)
                                {
                                    if (mainItem.StackSize > 1 && mainItem.StackSize <= infos.BaseInformation.StackSize)
                                    {
                                        if (client.Inventory.Count < 40)
                                        {
                                            ushort Amount = (ushort)usage.dwParam;
                                            mainItem.StackSize -= Amount;
                                            mainItem.Mode = Game.Enums.ItemMode.Update;
                                            mainItem.Send(client);
                                            mainItem.Mode = Game.Enums.ItemMode.Default;
                                            minorItem.ID = mainItem.ID;
                                            minorItem.Bound = mainItem.Bound;
                                            minorItem.Plus = mainItem.Plus;
                                            minorItem.Bless = mainItem.Bless;
                                            minorItem.StackSize = Amount;
                                            minorItem.Durability = mainItem.Durability;
                                            minorItem.MaximDurability = mainItem.MaximDurability;
                                            client.Inventory.Add(minorItem, Game.Enums.ItemUse.CreateAndAdd);
                                            Database.ConquerItemTable.UpdateStack(mainItem);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    case ItemUsage.Mode.MergeStackableItems:
                        {
                            //Jheison, you still owe me.
                            ConquerItem mainItem = null;
                            ConquerItem minorItem = new ConquerItem(true);
                            Database.ConquerItemInformation infos = null;
                            minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                            if (client.Inventory.TryGetItem(usage.UID, out mainItem) && client.Inventory.TryGetItem(usage.dwParam, out minorItem))
                            {
                                if (mainItem.UID != minorItem.UID && mainItem.ID == minorItem.ID && mainItem.Bound == minorItem.Bound)
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
                    //case ItemUsage.Mode.SplitStack:
                    //    {
                    //        ConquerItem mainItem = null;
                    //        ConquerItem minorItem = new ConquerItem(true);
                    //        Database.ConquerItemInformation infos = null;
                    //        minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                    //        if (client.Inventory.TryGetItem(usage.UID, out mainItem))
                    //        {
                    //            infos = new Database.ConquerItemInformation(mainItem.ID, 0);
                    //            if (mainItem.StackSize > 1 && mainItem.StackSize <= infos.BaseInformation.StackSize)
                    //            {
                    //                if (client.Inventory.Count < 40)
                    //                {
                    //                    ushort Amount = (ushort)usage.dwParam;

                    //                    mainItem.StackSize -= Amount;
                    //                    mainItem.Mode = Game.Enums.ItemMode.Update;
                    //                    mainItem.Send(client);
                    //                    mainItem.Mode = Game.Enums.ItemMode.Default;
                    //                    Database.ConquerItemTable.UpdateStack(mainItem);
                    //                    minorItem.ID = mainItem.ID;
                    //                    minorItem.StackSize += Amount;
                    //                    minorItem.Durability = mainItem.Durability;
                    //                    client.Inventory.Add(minorItem, Game.Enums.ItemUse.CreateAndAdd);
                    //                }
                    //            }
                    //        }
                    //        break;
                    //    }
                    //case ItemUsage.Mode.MergeStackableItems:
                    //    {
                    //        ConquerItem mainItem = null;
                    //        ConquerItem minorItem = new ConquerItem(true);
                    //        Database.ConquerItemInformation infos = null;
                    //        minorItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                    //        if (client.Inventory.TryGetItem(usage.UID, out mainItem) && client.Inventory.TryGetItem(usage.dwParam, out minorItem))
                    //        {
                    //            if (mainItem.ID == minorItem.ID)
                    //            {
                    //                infos = new Database.ConquerItemInformation(mainItem.ID, 0);
                    //                if (mainItem.StackSize < 1)
                    //                    mainItem.StackSize = 1;
                    //                if (minorItem.StackSize < 1)
                    //                    minorItem.StackSize = 1;

                    //                if ((mainItem.StackSize + minorItem.StackSize) <= infos.BaseInformation.StackSize)
                    //                {
                    //                    mainItem.StackSize += minorItem.StackSize;
                    //                    mainItem.Mode = Game.Enums.ItemMode.Update;
                    //                    mainItem.Send(client);
                    //                    mainItem.Mode = Game.Enums.ItemMode.Default;
                    //                    Database.ConquerItemTable.UpdateStack(mainItem);
                    //                    client.Inventory.Remove(minorItem, Game.Enums.ItemUse.Remove);
                    //                }
                    //            }
                    //        }
                    //        break;
                    //    }
                    case ItemUsage.Mode.Unknown:
                        {
                            break;
                        }
                    case ItemUsage.Mode.LowerEquipment:
                        Game.Features.EquipmentUpgrades.LowerLevel(client,usage.UID);
                        break;
                    default: if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                            client.Send(new Message("[ItemUsage] Unknown Action: " + usage.Action, Message.System)); break;
                }
            }
        }

        
    }
}
 
#endregion
