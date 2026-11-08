using System;
using System.IO;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe static class ShopFile
    {
        public static Dictionary<uint, Shop> Shops;
        public class Shop
        {
            public uint UID;
            public MoneyType MoneyType;
            public int Count { get { return Items.Count; } }
            public List<uint> Items;
        }
        public static void Load()
        {
            Console.WriteLine("Loading Shops information!");

            string[] text = File.ReadAllLines(Constants.ShopsPath);
            Shop shop = new Shop();
            for (int x = 0; x < text.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                string line = text[x];
                string[] split = line.Split('=');
                if (split[0] == "Amount")
                    Shops = new Dictionary<uint, Shop>(int.Parse(split[1]));
                else if (split[0] == "ID")
                {
                    uint id = uint.Parse(split[1]);
                    if (Shops.ContainsKey(id))
                        shop = Shops[id];
                    else
                    {
                        shop = new Shop();
                        shop.UID = id;
                        Shops.Add(id, shop);
                    }
                }
                else if (split[0] == "MoneyType")
                {
                    shop.MoneyType = (MoneyType)byte.Parse(split[1]);
                }
                else if (split[0] == "ItemAmount")
                {
                    shop.Items = new List<uint>(ushort.Parse(split[1]));
                }
                else if (split[0].Contains("Item") && split[0] != "ItemAmount")
                {
                    uint ID = uint.Parse(split[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0]);
                    if (!shop.Items.Contains(ID))
                        shop.Items.Add(ID);
                }
            }
            if (!Shops.ContainsKey(shop.UID))
                Shops.Add(shop.UID, shop);
            //Console.WriteLine("Done!");
        }
        public enum MoneyType
        {
            Gold = 0,
            ConquerPoints = 1,
            HonorPoints = 2,
            BoundCps = 3
        }
    }
}