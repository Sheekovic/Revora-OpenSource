using System;
using System.Collections.Generic;
using System.Linq;

namespace Revora.Database
{
    public class GoldenTreeTable
    {
        public class GoldenTreeCondition
        {
            public uint ID;
            public uint Type;
            public byte CostType;
            public uint CostItemID;
            public byte Monopoly;
            public byte CD;
            public byte CDCost;
            public DateTime StartTime;
        }
        public class GoldenTreePool
        {
            public uint ID;
            public uint Type;
            public byte PerfectLevel;
            public string Name;
            public uint ItemID;
        }
        public static Dictionary<uint, GoldenTreeCondition> Conditions = new Dictionary<uint, GoldenTreeCondition>();
        public static Dictionary<uint, GoldenTreePool> Pools = new Dictionary<uint, GoldenTreePool>();
        public static void Load()
        {
            #region GoldenLotteryCondition
            Conditions = new Dictionary<uint, GoldenTreeCondition>();
            var r = new IniFile("database\\GoldenTree\\globallotterycondition.ini");
            int count = r.ReadInt32("ConditionAmount", "Amount", 0);
            for (uint i = 0; i < count; i++)
            {
                GoldenTreeCondition prize = new GoldenTreeCondition();
                prize.ID = r.ReadUInt32(i.ToString(), "id");
                prize.Type = r.ReadUInt32(i.ToString(), "Type");
                prize.CostType = r.ReadByte(i.ToString(), "CostType", 0);
                prize.CostItemID = r.ReadUInt32(i.ToString(), "CostValue");
                prize.Monopoly = r.ReadByte(i.ToString(), "Monopoly", 0);
                prize.CD = r.ReadByte(i.ToString(), "CD", 0);
                prize.CDCost = r.ReadByte(i.ToString(), "CDCost", 0);
                prize.StartTime = Kernel.FromDateTimeInt(ulong.Parse(r.ReadString(i.ToString(), "StartTime")));
                Conditions.Add(prize.ID, prize);
            }
            #endregion
            #region GoldenLotteryPool
            string[] file = DatCryption.Decrypt(("database\\GoldenTree\\global_lottery_pool.dat"));
            //var file = new DatCryption().Decrypt(Environment.CurrentDirectory + "\\database\\global_lottery_pool.dat");
            foreach (var line1 in file)
            {
                var spilit = line1.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                GoldenTreePool pool = new GoldenTreePool();
                pool.ID = uint.Parse(spilit[0]);
                pool.Type = uint.Parse(spilit[1]);
                pool.PerfectLevel = byte.Parse(spilit[2]);
                pool.Name = spilit[3];
                pool.ItemID = uint.Parse(spilit[4]);
                Pools.Add(pool.ID, pool);
            }
            #endregion
        }
    }
}
