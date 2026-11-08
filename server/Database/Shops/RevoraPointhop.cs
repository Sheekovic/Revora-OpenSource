// * Authorized for personal/commerical use
// * Open Source
// * REVORA [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe static class REVORAPointhop
    {
        public const uint UID = 6001;
        public static REVORAPointhopBase Shop;
        public class RacePointItem
        {
            public uint ID, Price;
        }
        public class REVORAPointhopBase
        {
            public int Count { get { return Items.Count; } }
            public Dictionary<uint, RacePointItem> Items;
        }
        public static void Load()
        {
            Console.WriteLine("Loading RacePoint shop information");
            string[] text = File.ReadAllLines(Constants.RaceShopPath);
            Shop = new REVORAPointhopBase();
            Shop.Items = new Dictionary<uint, RacePointItem>();
            for (int x = 0; x < text.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                string line = text[x].ToLower();
                if (line.StartsWith("[normal]"))
                {
                    continue;
                }
                else if (line.StartsWith("[recommend]"))
                {
                    break;
                }
                else
                {
                    if (line.Length > 3)
                    {
                        string[] Parts = line.Split(';')[0].Split(' ');
                        RacePointItem item = new RacePointItem();
                        item.ID = uint.Parse(Parts[0]);
                        item.Price = uint.Parse(Parts[1]);
                        Shop.Items.Add(item.ID, item);
                    }
                }
            }
            //Console.WriteLine("Done!");
        }
    }
}