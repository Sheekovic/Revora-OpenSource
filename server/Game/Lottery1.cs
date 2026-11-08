using System;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public unsafe class Lottery1
    {
        public static void LotteryRewardMessage(Client.GameState Hero, ConquerItem i)
        {
            string str = "";
            string str2 = "";
            string str3 = "";
            if ((Game.Enums.ItemQuality)(i.ID % 10) >= Game.Enums.ItemQuality.Elite)
            {
                str = "Elite ";
                if ((Game.Enums.ItemQuality)(i.ID % 10) >= Game.Enums.ItemQuality.Super)
                {
                    str = "Super ";
                }
                if (Database.ConquerItemInformation.BaseInformations[i.ID].Name.Contains("MoneyBag"))
                {
                    str = "";
                }
            }
            uint ID = i.ID;
            if (i.SocketOne > 0)
            {
                if (ID <= 181305 && ID >= 194300)
                {
                    if (ID <= 700001 && ID >= 700123)
                    {
                        if (ID != 1088000 || ID != 1088001 || ID != 1088002)
                        {
                            str2 = "1-Socket ";
                            if (i.SocketTwo > 0)
                            {
                                str2 = "2-Socket ";
                            }
                            return;
                        }
                    }
                }
            }
            if (i.Plus > 0)
            {
                str3 = "(+" + i.Plus + ")";
            }
            string str4 = str + str2 + Database.ConquerItemInformation.BaseInformations[i.ID].Name + str3;
            string msg = string.Format("{0} Recibe {1} de la Loteria de Aniversario!", Hero.Entity.Name, str4);
            if ((Game.Enums.ItemQuality)(i.ID % 10) >= Game.Enums.ItemQuality.Elite)
            {
                Kernel.SendWorldMessage(new Message(msg, System.Drawing.Color.Red, Network.GamePackets.Message.Talk), Server.GamePool);
            }
            else
            {
                Hero.Send(new Message(msg, System.Drawing.Color.White, Message.Talk));
            }
        }
        public static void GiveLotteryPrize(Client.GameState c)
        {
            LotteryRewardMessage(c, c.Entity.LotteryPrize);
            Database.ConquerItemInformation Itemd = new Database.ConquerItemInformation(c.Entity.LotteryItemID, 0);
            ConquerItem Item = new ConquerItem(true);
            Item.ID = c.Entity.LotteryItemID;
            Item.Plus = (byte)c.Entity.LotteryItemPlus;
            Item.Color = Game.Enums.Color.Blue;
            if (c.Entity.LotteryItemSoc1 > 0)
            {
                Item.SocketOne = Game.Enums.Gem.EmptySocket;
                c.Entity.LotteryItemSoc1 = 0;
            }
            if (c.Entity.LotteryItemSoc2 > 0)
            {
                Item.SocketTwo = Game.Enums.Gem.EmptySocket;
                c.Entity.LotteryItemSoc2 = 0;
            }
            Item.Durability = Item.MaximDurability = Itemd.BaseInformation.Durability;
            c.Inventory.Add(Item, Game.Enums.ItemUse.CreateAndAdd);
            c.Entity.LotteryItemID = 0;
            c.Entity.LotteryJadeAdd = 0;
        }
        public static void CajasSlayer(uint npcID, Client.GameState h, bool jade = false)
        {
            if (h.Entity.ConquerPoints >= 0 || jade)
            {
                if (jade || h.Inventory.Contains(3006048, 1))
                {
                    if (!jade)
                    {
                        h.LotteryEntries++;
                    }
                    else
                    {
                        h.Entity.LotteryJadeAdd++;
                    }
                    if ((npcID != 0) && !jade)
                    {
                        _String packet = new _String(true);
                        packet.UID = npcID;
                        packet.TextsCount = 1;
                        packet.Type = _String.Effect;
                        packet.Texts.Add("lotterySlayer");
                        h.Send(packet);
                        if (h.Entity.LotteryItemID > 0)
                        {

                        }
                    }
                tryagain:
                    int rand = Kernel.Random.Next(Database.LotterySlayer.LotteryItems.Count);
                    var item = Database.LotterySlayer.LotteryItems[rand];
                    var Itemd = Database.ConquerItemInformation.BaseInformations[item.ID];
                    if (Itemd == null)
                        goto tryagain;

                    if (Kernel.Rate(item.Rank, item.Chance) && Kernel.Rate(item.Rank, 35 - item.Rank))
                    {
                        ConquerItem Item = new ConquerItem(true);
                        Item.ID = item.ID;
                        h.Entity.LotteryItemID = item.ID;
                        h.Entity.LotteryItemPlus = item.Plus;
                        h.Entity.LotteryItemColor = (byte)Game.Enums.Color.Blue;
                        Item.Plus = item.Plus;
                        Item.Color = Game.Enums.Color.Blue;
                        if (item.Sockets > 0)
                        {
                            Item.SocketOne = Game.Enums.Gem.EmptySocket;
                            h.Entity.LotteryItemSoc1 = 255;
                        }
                        if (item.Sockets > 1)
                        {
                            Item.SocketTwo = Game.Enums.Gem.EmptySocket;
                            h.Entity.LotteryItemSoc2 = 255;
                        }
                        Item.Durability = Item.MaximDurability = Itemd.Durability;
                        if ((Item != null))
                        {
                            if (!jade)
                            {
                                if (h.Inventory.Contains(3006048, 1))
                                {

                                    h.Inventory.Remove(3006048, 1);
                                }
                                else
                                {
                                    return;
                                }
                            }
                            else
                            {
                                if (h.Inventory.Contains(3006048, 1))
                                {

                                    h.Inventory.Remove(3006048, 1);
                                }
                                else
                                {
                                    return;
                                }
                            }
                            h.Entity.LotteryPrize = Item;
                            Network.GamePackets.Lottery lottery = new Network.GamePackets.Lottery
                            {
                                Color = Item.Color,
                                Plus = Item.Plus,
                                Prize = Item.ID,
                                SocketOne = (byte)Item.SocketOne,
                                SocketTwo = (byte)Item.SocketTwo,
                                AddJadeChances = h.Entity.LotteryJadeAdd,
                                Type = Network.GamePackets.Lottery.LotteryTypes.ShowGUI
                            };
                            lottery.Send(h);

                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        goto tryagain;
                    }
                }
                else
                {
                    string msg = string.Format("Para destapar esta loteria nesecitas TarjetaDeAniversario!", h.Entity.Name, h.Entity.Name);
                    h.Send(new Message(msg, System.Drawing.Color.White, Message.Talk));
                }
            }
            else
            {
                string msg = string.Format("Para empezar Ve al Npc de Aniversario y compra TarjetaAniversario!", h.Entity.Name, h.Entity.Name);
                h.Send(new Message(msg, System.Drawing.Color.White, Message.Talk));
            }
        }       
        public static void Process(byte[] Packet, Client.GameState Client)
        {
            Network.GamePackets.Lottery1 lottery1 = new Network.GamePackets.Lottery1();
            lottery1.Deserialize(Packet);
            if (Client != null)
            {
                switch (lottery1.Type)
                {
                    case Network.GamePackets.Lottery1.LotteryTypes.Accept:
                        {
                            if (Client.Inventory.Contains(3006048, 1))
                            {
                                CajasSlayer(0, Client, true);
                            }
                            break;
                        }
                    case Network.GamePackets.Lottery1.LotteryTypes.AddJade:
                        {
                            if (Client.Inventory.Contains(3006048, 1))
                            {
                                CajasSlayer(0, Client, true);
                            }
                            break;
                        }
                    case Network.GamePackets.Lottery1.LotteryTypes.Continue:
                        {
                            if (Client.Inventory.Contains(3006048, 1))
                            {
                                CajasSlayer(0, Client, true);
                            }
                            break;
                        }
                }
            }
        }
    }
}