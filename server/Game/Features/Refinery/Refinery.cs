using System;
using Revora.Database;

namespace Revora
{
    public unsafe class Refinery
    {
        public class RefineryItem
        {
            public UInt32 Identifier, Position, Percent;
            public Byte Level;
            public Boolean Untradable;
            public RefineryType Type;
            public enum RefineryType
            {
                MDefence = 1,
                Critical = 2,
                SCritical = 3,
                Immunity = 4,
                BreakThrough = 5,
                Counteraction = 6,
                Detoxication = 7,
                Block = 8,
                Penetration = 9,
                Intensification = 11
            }
        }
        public class RefineryBoxes
        {
            public UInt32 Identifier, Position;
            public Boolean Untradable;
            public RefineryItem.RefineryType Type;
        }
        public static void LoadItems()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("refinery"))
            using (var reader = cmd.CreateReader())
            {
                Console.WriteLine("Loading Refinery Items");
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    RefineryItem ri = new RefineryItem();
                    ri.Identifier = reader.ReadUInt32("Identifier");
                    ri.Level = reader.ReadByte("Level");
                    ri.Percent = reader.ReadUInt32("Percent");
                    ri.Position = reader.ReadUInt32("Position");
                    ri.Type = (RefineryItem.RefineryType)reader.ReadUInt32("RefineType");
                    ri.Untradable = reader.ReadBoolean("Untradable");
                    Kernel.DatabaseRefinery.Add(ri.Identifier, ri);
                }
            }
            //Console.WriteLine("Done!");
            LoadBoxes();
        }
        public static void LoadBoxes()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("refineryboxes"))
            using (var reader = cmd.CreateReader())
            {
                Console.WriteLine("Loading Refinery Boxes!");
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    RefineryBoxes ri = new RefineryBoxes();
                    ri.Identifier = reader.ReadUInt32("Identifier");
                    ri.Position = reader.ReadUInt32("Position");
                    ri.Type = (RefineryItem.RefineryType)reader.ReadUInt32("RefineType");
                    ri.Untradable = reader.ReadBoolean("Untradable");
                    Kernel.DatabaseRefineryBoxes.Add(ri.Identifier, ri);
                }
            }
            //Console.WriteLine("Done!");
        }
    }
}