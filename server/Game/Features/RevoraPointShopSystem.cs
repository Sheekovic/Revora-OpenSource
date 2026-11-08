using System;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class RevoraPointShopSystem
    {
        public const uint NpcId = 900000014;
        public const string NpcName = "Vesper";
        public const ushort TwinCityMap = 1002;
        public const ushort ClaimX = 396;
        public const ushort ClaimY = 340;
        public const ushort Mesh = 50110;

        private struct Good
        {
            public byte Option;
            public uint Id;
            public uint Price;
            public string Label;

            public Good(byte option, uint id, uint price, string label)
            {
                Option = option;
                Id = id;
                Price = price;
                Label = label;
            }
        }

        private static readonly Good[] Stones = new Good[]
        {
            new Good(11, 3009001, 400, "Bright Star Stone"),
            new Good(12, 3009002, 2000, "Radiant Star Stone"),
            new Good(13, 3009003, 8000, "Splendid Star Stone")
        };

        private static readonly Good[] Runes = new Good[]
        {
            new Good(180, 4010001, 1000, "Comprehension (Trojan)"),
            new Good(181, 4010101, 1000, "Pitching (Warrior)"),
            new Good(182, 4010201, 1000, "Frost Arrows (Archer)"),
            new Good(183, 4010301, 1000, "Assassin (Ninja)"),
            new Good(184, 4010401, 1000, "Acalanatha (Monk)"),
            new Good(185, 4010501, 1000, "Tide Trap (Pirate)"),
            new Good(186, 4010601, 1000, "Swinging Tail (Dragon Warrior)"),
            new Good(187, 4010701, 1000, "Sacrifice (Water Taoist)"),
            new Good(188, 4010801, 1000, "Burning Sky (Fire Taoist)"),
            new Good(189, 4010901, 1000, "Revenge Gale (Windwalker)"),
            new Good(190, 4020101, 750, "Rampage (Trojan)"),
            new Good(191, 4020201, 750, "All-round (Warrior)"),
            new Good(192, 4020301, 750, "Infinity (Archer)"),
            new Good(193, 4020401, 750, "Absolution (Ninja)"),
            new Good(194, 4020501, 750, "Iron Shield (Monk)"),
            new Good(195, 4020601, 750, "Slayer (Pirate)"),
            new Good(196, 4020701, 750, "Counter Punch (Dragon Warrior)"),
            new Good(197, 4020801, 750, "Fine Rain (Water Taoist)"),
            new Good(198, 4020901, 750, "Fire Curse (Fire Taoist)"),
            new Good(199, 4021001, 750, "Wild Wind (Windwalker)"),
            new Good(200, 4030101, 500, "Tortoise Breaker"),
            new Good(201, 4030201, 500, "Draining Touch Booster"),
            new Good(202, 4030301, 500, "Blood Spawn Booster"),
            new Good(203, 4030401, 500, "Healer"),
            new Good(204, 4030501, 500, "Sturdiness"),
            new Good(205, 4030601, 500, "XP Killer"),
            new Good(206, 4030701, 500, "Grabber"),
            new Good(207, 4030801, 500, "MP Master"),
            new Good(208, 4030901, 500, "Boss Killer"),
            new Good(209, 4031001, 500, "No Mercy"),
            new Good(210, 4031101, 500, "XP Booster"),
            new Good(211, 4031201, 500, "Free Soul Booster"),
            new Good(212, 4031301, 500, "Witchery")
        };

        private static readonly Good[] WeaponSouls = new Good[]
        {
            new Good(30, 800020, 12000, "P7 MonsterSaber"),
            new Good(31, 800111, 12000, "P7 SkyHammer"),
            new Good(32, 800142, 12000, "P7 ShadowKatana"),
            new Good(33, 800215, 12000, "P7 SkyHalberd"),
            new Good(34, 800522, 12000, "P7 TimeBacksword"),
            new Good(35, 800618, 12000, "P7 SunBow"),
            new Good(36, 800725, 12000, "P7 BuddaBeads"),
            new Good(37, 800810, 12000, "P7 DeathPistol"),
            new Good(38, 801004, 12000, "P7 WarCraze"),
            new Good(39, 801104, 12000, "P7 WonderHossu")
        };

        private static readonly Good[] ArmorSouls = new Good[]
        {
            new Good(50, 820073, 10000, "P7 MoonHeadgear"),
            new Good(51, 820076, 10000, "P7 IceHeadgear"),
            new Good(52, 822071, 10000, "P7 NetherArmor"),
            new Good(53, 822072, 10000, "P7 EclipseArmor"),
            new Good(54, 821033, 10000, "P7 HeavenNecklace"),
            new Good(55, 821034, 10000, "P7 FervorBag"),
            new Good(56, 823058, 10000, "P7 CraneRing"),
            new Good(57, 823060, 10000, "P7 RainbowBracelet"),
            new Good(58, 824018, 10000, "P7 FoxBoots"),
            new Good(59, 824020, 10000, "P7 CraneBoots")
        };

        private static readonly Good[] Garments = new Good[]
        {
            new Good(70, 189695, 2500, "Monkey King Armor"),
            new Good(71, 194875, 2500, "Bright Purple Romance"),
            new Good(72, 187405, 2500, "Armor of Holy Light"),
            new Good(73, 189065, 2500, "Robe of Darkness (Hades)"),
            new Good(74, 188915, 2500, "Gold Cloth (Saint)"),
            new Good(75, 193445, 2500, "Frozen Fantasy (Glaze)"),
            new Good(60, 193525, 2500, "Frozen Fantasy (Glory)"),
            new Good(61, 193565, 2500, "Lava Cat Robe (Star)"),
            new Good(62, 193625, 2500, "Butterfly Rose"),
            new Good(63, 194395, 2500, "Lunar Angelic Robe"),
            new Good(64, 189675, 2500, "Bubble Swimsuit")
        };

        private static readonly Good[] Mounts = new Good[]
        {
            new Good(80, 200517, 2500, "King of Scorpions (Saint)"),
            new Good(81, 200531, 2500, "Fiery Dragon (Hades)"),
            new Good(82, 200613, 2500, "Fierce Cloud"),
            new Good(83, 200614, 2500, "Pacific Cloud"),
            new Good(84, 200623, 2500, "Radiant Golden Retriever"),
            new Good(85, 200624, 2500, "Superdog"),
            new Good(86, 200564, 2500, "Super Victor Alpaca"),
            new Good(87, 200572, 2500, "Pure Lotus"),
            new Good(88, 200573, 2500, "Radiant Lotus"),
            new Good(89, 200575, 2500, "Astral Phoenix"),
            new Good(90, 200537, 2500, "Holy Lotus (Goddess)"),
            new Good(91, 200540, 2500, "Savage Beast"),
            new Good(92, 200482, 2500, "Winged Frostcat Armor"),
            new Good(93, 200449, 2500, "Somersault Cloud"),
            new Good(94, 200559, 2500, "Solar Monkey"),
            new Good(95, 200560, 2500, "Lunar Monkey"),
            new Good(96, 200544, 2500, "General Cat (Star)"),
            new Good(97, 200580, 2500, "Mythic Beast"),
            new Good(98, 200494, 2500, "Money Horse"),
            new Good(99, 200592, 2500, "Star Rooster Ultimate Challenge"),
            new Good(100, 200596, 2500, "Celestial Fox (Fantasy)"),
            new Good(101, 200524, 2500, "Gentle Antelope [Exclusive]")
        };

        public static void Create()
        {
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, ClaimX, ClaimY, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            else
                Console.WriteLine(NpcName + " spawn skipped: map 1002 not ready");
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            if (!Ready(client)) return true;
            Npcs dialog = new Npcs(client);
            byte opt = request.OptionID;
            switch (opt)
            {
                case 0:
                    Greeting(client, dialog);
                    return true;
                case 1:
                    ListGoods(client, dialog, "Perfection stones. Bright 100 progress, Radiant 1,000, Splendid 10,000.", Stones);
                    return true;
                case 20:
                    dialog.Text("Level 1 runes. Red: 1,000 RVPs. Blue: 750 RVPs. Yellow: 500 RVPs. Class requirements still apply when equipping.");
                    dialog.Option("Red runes", 110);
                    dialog.Option("Blue runes", 111);
                    dialog.Option("Yellow runes", 112);
                    dialog.Option("Back", 0);
                    dialog.Send();
                    return true;
                case 110: ListRunePage(client, dialog, 0, 0); return true;
                case 113: ListRunePage(client, dialog, 0, 1); return true;
                case 111: ListRunePage(client, dialog, 1, 0); return true;
                case 114: ListRunePage(client, dialog, 1, 1); return true;
                case 112: ListRunePage(client, dialog, 2, 0); return true;
                case 115: ListRunePage(client, dialog, 2, 1); return true;
                case 116: ListRunePage(client, dialog, 2, 2); return true;
                case 2:
                    ListGoods(client, dialog, "P7 weapon souls. Attach them yourself.", WeaponSouls);
                    return true;
                case 4:
                    ListGoods(client, dialog, "P7 armor and accessory souls.", ArmorSouls);
                    return true;
                case 5:
                    ListGarmentPage(client, dialog, 0);
                    return true;
                case 8:
                    ListGarmentPage(client, dialog, 1);
                    return true;
                case 6:
                    ListMountPage(client, dialog, 0);
                    return true;
                case 7:
                    ListMountPage(client, dialog, 1);
                    return true;
                case 9:
                    ListMountPage(client, dialog, 2);
                    return true;
                case 10:
                    ListMountPage(client, dialog, 3);
                    return true;
            }
            if (TryBuy(client, dialog, opt))
                return true;
            Greeting(client, dialog);
            return true;
        }

        private static void Greeting(GameState client, Npcs dialog)
        {
            dialog.Text("I am Vesper. Twin City's high stall. You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
            dialog.Text("P7 souls, Perfection stones, 5-star garments and mounts, and runes. I name the price before I take a coin.");
            dialog.Option("Perfection stones", 1);
            dialog.Option("Runes", 20);
            dialog.Option("P7 weapon souls", 2);
            dialog.Option("P7 armor souls", 4);
            dialog.Option("5-star garments", 5);
            dialog.Option("5-star mounts", 6);
            dialog.Option("Goodbye", 255);
            dialog.Send();
        }

        private static void ListGoods(GameState client, Npcs dialog, string header, Good[] goods)
        {
            dialog.Text(header + " You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
            int listed = 0;
            for (int i = 0; i < goods.Length; i++)
            {
                if (!InCatalog(goods[i].Id))
                    continue;
                dialog.Option(goods[i].Label + " - " + Rp(goods[i].Price), goods[i].Option);
                listed++;
            }
            if (listed == 0)
                dialog.Text("That shelf is empty in this world's catalog.");
            dialog.Option("Back", 0);
            dialog.Option("Goodbye", 255);
            dialog.Send();
        }

        private static void ListGarmentPage(GameState client, Npcs dialog, int page)
        {
            const int pageSize = 6;
            int start = page * pageSize;
            dialog.Text("5-star garments from the new catalog. You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
            int listed = 0;
            int end = start + pageSize;
            if (end > Garments.Length)
                end = Garments.Length;
            for (int i = start; i < end; i++)
            {
                if (!InCatalog(Garments[i].Id))
                    continue;
                dialog.Option(Garments[i].Label + " - " + Rp(Garments[i].Price), Garments[i].Option);
                listed++;
            }
            if (listed == 0)
                dialog.Text("That shelf is empty in this world's catalog.");
            if (end < Garments.Length)
                dialog.Option("More 5-star garments", 8);
            if (page > 0)
                dialog.Option("Previous garments", 5);
            dialog.Option("Back", 0);
            dialog.Option("Goodbye", 255);
            dialog.Send();
        }

        private static void ListMountPage(GameState client, Npcs dialog, int page)
        {
            const int pageSize = 6;
            int start = page * pageSize;
            dialog.Text("5-star mounts from the new catalog. You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
            int listed = 0;
            int end = start + pageSize;
            if (end > Mounts.Length)
                end = Mounts.Length;
            for (int i = start; i < end; i++)
            {
                if (!InCatalog(Mounts[i].Id))
                    continue;
                dialog.Option(Mounts[i].Label + " - " + Rp(Mounts[i].Price), Mounts[i].Option);
                listed++;
            }
            if (listed == 0)
                dialog.Text("That shelf is empty in this world's catalog.");
            if (end < Mounts.Length)
            {
                byte more = 7;
                if (page == 1) more = 9;
                else if (page == 2) more = 10;
                dialog.Option("More 5-star mounts", more);
            }
            if (page > 0)
            {
                byte prev = 6;
                if (page == 2) prev = 7;
                else if (page == 3) prev = 9;
                dialog.Option("Previous mounts", prev);
            }
            dialog.Option("Back", 0);
            dialog.Option("Goodbye", 255);
            dialog.Send();
        }

        private static void ListRunePage(GameState client, Npcs dialog, int category, int page)
        {
            int start = category * 10;
            int count = category == 2 ? 13 : 10;
            byte[] pages = category == 0 ? new byte[] {110,113} : category == 1 ? new byte[] {111,114} : new byte[] {112,115,116};
            dialog.Text((category == 0 ? "Red" : category == 1 ? "Blue" : "Yellow") + " runes, level 1. You hold " + client.Entity.RevoraPoint + " RVPs.");
            for (int i = page * 5; i < Math.Min(count, page * 5 + 5); i++)
            {
                var good = Runes[start + i];
                if (InCatalog(good.Id)) dialog.Option(good.Label + " - " + good.Price + " RVPs", good.Option);
            }
            if (page > 0) dialog.Option("Previous", pages[page - 1]);
            if (page + 1 < pages.Length) dialog.Option("Next", pages[page + 1]);
            dialog.Option("Rune categories", 20);
            dialog.Send();
        }

        private static bool TryBuy(GameState client, Npcs dialog, byte option)
        {
            Good good;
            if (Match(Runes, option, out good))
            {
                bool bought = SubclassShop.BuyRune(client, good.Id, good.Price);
                dialog.Text(bought ? good.Label + " (level 1) is in your bag. Paid " + good.Price + " RVPs."
                    : "Purchase unavailable. Stand near Vesper with enough RVPs and a free inventory slot.");
                dialog.Option("More runes", 20);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return true;
            }
            if (!FindGood(option, out good))
                return false;
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return true;
            }
            if (!InCatalog(good.Id))
            {
                dialog.Text("That piece is missing from this world's catalog.");
                dialog.Option("Back", 0);
                dialog.Send();
                return true;
            }
            if (client.Inventory == null || client.Inventory.Count + 1 > 40)
            {
                dialog.Text("Clear a space in your inventory first. I will not take your Revora Points.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return true;
            }
            if (client.Entity.RevoraPoint < good.Price)
            {
                dialog.Text("That costs " + Rp(good.Price) + ". You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
                dialog.Option("Back", 0);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return true;
            }
            if (client.ItemSyncRoot == null)
                return true;
            lock (client.ItemSyncRoot)
            {
                if (!Ready(client)) return true;
                try
                {
                    if (client.Entity.PersistenceRequiresReload)
                        throw new InvalidOperationException("Vesper purchase is no longer ready.");
                    if (client.Entity.RevoraPoint < good.Price || client.Inventory == null || client.Inventory.Count + 1 > 40)
                    {
                        dialog.Text("That purchase is no longer available. Your Revora Points were not taken.");
                        dialog.Option("I see.", 255);
                        dialog.Send();
                        return true;
                    }
                    uint next = client.Entity.RevoraPoint - good.Price;
                    Good soul;
                    bool bound = Match(WeaponSouls, option, out soul) || Match(ArmorSouls, option, out soul);
                    ConquerItem item = QuestInventory.New(good.Id, bound);
                    using (var conn = DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {
                            long? savedCp, savedRvp;
                            PurseBalance.CopySaved(client, out savedCp, out savedRvp);
                            using (var q = QuestInventory.Command(conn, tx, "SELECT revorapoint FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            {
                                object value = q.ExecuteScalar();
                                long? db = value == null || value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
                                if (value == null || db != savedRvp)
                                    throw new InvalidOperationException("Vesper purse changed.");
                            }
                            if (QuestInventory.Execute(conn, tx, "UPDATE entities SET revorapoint=@rvp WHERE uid=@uid AND (CASE WHEN @isnull THEN revorapoint IS NULL ELSE revorapoint=@old END)",
                                "rvp", (long)next, "uid", (long)client.Entity.UID, "isnull", !savedRvp.HasValue, "old", savedRvp.GetValueOrDefault()) != 1)
                                throw new InvalidOperationException("Vesper purse changed.");
                            QuestInventory.Insert(conn, tx, client.Entity.UID, item);
                            if (!Ready(client)) throw new InvalidOperationException("Vesper buyer is no longer present.");
                            tx.Commit();
                        }
                    }
                    client.Entity.ApplyCommittedRevoraPoints(next);
                    QuestInventory.Install(client, new uint[0], new[] { item });
                }
                catch (Exception ex)
                {
                    QuestInventory.Fail(client, "vesper-purchase", ex);
                    dialog.Text("The purchase could not be confirmed. Please reconnect to check your bag and balance.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                }
            }
            dialog.Text(good.Label + " is in your bag. You hold [" + client.Entity.RevoraPoint + "] Revora Points.");
            dialog.Option("Shop more", 0);
            dialog.Option("Goodbye", 255);
            dialog.Send();
            client.Send(new Message("Vesper sold you " + good.Label + " for " + good.Price + " Revora Points.", Color.Yellow, Message.TopLeft));
            return true;
        }

        private static bool FindGood(byte option, out Good good)
        {
            if (Match(Stones, option, out good)) return true;
            if (Match(WeaponSouls, option, out good)) return true;
            if (Match(ArmorSouls, option, out good)) return true;
            if (Match(Garments, option, out good)) return true;
            if (Match(Mounts, option, out good)) return true;
            good = default(Good);
            return false;
        }

        private static bool Ready(GameState client)
        {
            return client != null && client.Map != null && QuestInventory.Near(client, NpcId, TwinCityMap);
        }

        private static bool Match(Good[] goods, byte option, out Good good)
        {
            for (int i = 0; i < goods.Length; i++)
            {
                if (goods[i].Option == option)
                {
                    good = goods[i];
                    return true;
                }
            }
            good = default(Good);
            return false;
        }

        private static bool InCatalog(uint id)
        {
            return ConquerItemInformation.BaseInformations.ContainsKey(id);
        }

        private static string Rp(uint amount)
        {
            return amount.ToString("N0") + " RP";
        }
    }
}
