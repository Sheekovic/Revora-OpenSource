using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Database
{
    public unsafe class LotterySlayer
    {
        public struct LotteryItem
        {
            public int Rank, Chance;
            public string Name;
            public uint ID;
            public byte Color;
            public byte Sockets;
            public byte Plus;
        }
        public static List<LotteryItem> LotteryItems = new List<LotteryItem>();
        public static void Load()
        {
            Console.WriteLine("Loading LotterySlayer information!");
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("lotterySlayer"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    LotteryItem item = new LotteryItem();
                    item.Rank = reader.ReadInt32("Rank");
                    item.Chance = reader.ReadInt32("Chance");
                    item.Name = reader.ReadString("Prize_Name");
                    item.ID = reader.ReadUInt32("Prize_Item");
                    item.Color = reader.ReadByte("Color");
                    item.Sockets = reader.ReadByte("Hole_Num");
                    item.Plus = reader.ReadByte("Addition_Lev");
                    LotteryItems.Add(item);
                }
            }
            //Console.WriteLine("Done!");
        }
    }
}
