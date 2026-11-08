using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Database
{
    public class ExchangeItems
    {
        public uint ID;
        public uint ItemID;
        public uint UnKnown;
        public uint Remaining;
        public bool Bound;
        public uint NeededItemID;
        public uint NeddedItemCount;
    }
    public static class ExchangueShopFile
    {
        public static Dictionary<uint, ExchangeItems> Items;
        public static void Load()
        {
            Console.WriteLine2("Loading Exchange Items information...  ");
            string[] text = File.ReadAllLines(Constants.ExchangeShopsPath);
            Items = new Dictionary<uint, ExchangeItems>();
            ExchangeItems Item;
            for (int x = 0; x < text.Length; x++)
            {
              
                string line = text[x];
                string[] split = line.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                Item = new ExchangeItems();
                Item.ID = uint.Parse(split[0]);
                Item.ItemID = uint.Parse(split[1]);
                Item.UnKnown = uint.Parse(split[2]);
                Item.Remaining = uint.Parse(split[3]);
                Item.Bound = uint.Parse(split[4]) != 0 ? true : false;
                Item.NeededItemID = uint.Parse(split[5]);
                Item.NeddedItemCount = uint.Parse(split[6]);
                if (!Items.ContainsKey(Item.ID))
                {
                    Items.Add(Item.ID, Item);
                }
            }
        }
    }
}
