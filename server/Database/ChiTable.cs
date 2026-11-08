using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Revora.Game.ConquerStructures;
using Revora.Game;
using System.Collections.Concurrent;

namespace Revora.Database
{
    public class ChiTable
    {
        public class ChiData
        {
            public uint UID;
            public string Name;
            public List<ChiPowerStructure> Powers;

            public int DragonRank, PhoenixRank, TigerRank, TurtleRank;

            public int DragonPoints
            {
                get
                {
                    if (Powers.Count > 0)
                        return Powers[0].Points;
                    return 0;
                }
            }
            public int PhoenixPoints
            {
                get
                {
                    if (Powers.Count > 1)
                        return Powers[1].Points;
                    return 0;
                }
            }
            public int TigerPoints
            {
                get
                {
                    if (Powers.Count > 2)
                        return Powers[2].Points;
                    return 0;
                }
            }
            public int TurtlePoints
            {
                get
                {
                    if (Powers.Count > 3)
                        return Powers[3].Points;
                    return 0;
                }
            }

            internal uint SelectRank(Enums.ChiPowerType chiPowerType)
            {
                switch (chiPowerType)
                {
                    case Enums.ChiPowerType.Dragon:
                        return (uint)DragonRank;
                    case Enums.ChiPowerType.Phoenix:
                        return (uint)PhoenixRank;
                    case Enums.ChiPowerType.Tiger:
                        return (uint)TigerRank;
                    case Enums.ChiPowerType.Turtle:
                        return (uint)TurtleRank;
                }
                return 0;
            }

            internal uint SelectPoints(Enums.ChiPowerType chiPowerType)
            {
                switch (chiPowerType)
                {
                    case Enums.ChiPowerType.Dragon:
                        return (uint)DragonPoints;
                    case Enums.ChiPowerType.Phoenix:
                        return (uint)PhoenixPoints;
                    case Enums.ChiPowerType.Tiger:
                        return (uint)TigerPoints;
                    case Enums.ChiPowerType.Turtle:
                        return (uint)TurtlePoints;
                }
                return 0;
            }
        }
        public static ConcurrentDictionary<uint, ChiData> AllData = new ConcurrentDictionary<uint, ChiData>();
        public static ChiData[] Dragon, Phoenix, Tiger, Turtle;

        public static void LoadAllChi()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("chi");
                using (MySqlReader rdr = new MySqlReader(cmd))
                {
                    while (rdr.Read())
                    {
                        ChiData chiData = new ChiData();
                        chiData.UID = rdr.ReadUInt32("uid");
                        chiData.Name = rdr.ReadString("name");
                        chiData.Powers = new List<ChiPowerStructure>();
                        byte[] data = rdr.ReadBlob("chipowers");
                        if (data.Length > 0)
                        {
                            using (var stream = new MemoryStream(data))
                            using (var reader = new BinaryReader(stream))
                            {
                                int count = reader.ReadByte();
                                for (int i = 0; i < count; i++)
                                    chiData.Powers.Add(new ChiPowerStructure().Deserialize(reader));
                            }
                        }
                        AllData[chiData.UID] = chiData;
                    }
                }
            }
        }
        public static void Load(Client.GameState client)
        {
            lock (client.ItemSyncRoot)
            {
                Revora.Game.Features.ChiWallet.Forget(client);
                client.ChiPowers.Clear();
                client.Retretead_ChiPowers.Clear();
                LoadCore(client);
                Revora.Game.Features.ChiWallet.Loaded(client, client.ChiPoints);
            }
        }
        private static void LoadCore(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("chi").Where("uid", client.Entity.UID);
                using (MySqlReader rdr = new MySqlReader(cmd))
                {
                    if (rdr.Read())
                    {
                        client.ChiPoints = rdr.ReadUInt32("points");
                        byte[] data = rdr.ReadBlob("chipowers");
                        if (data.Length > 0)
                        {
                            using (var stream = new MemoryStream(data))
                            using (var reader = new BinaryReader(stream))
                            {
                                int count = reader.ReadByte();
                                for (int i = 0; i < count; i++)
                                {
                                    var power = new ChiPowerStructure().Deserialize(reader);
                                    if (power.Power == (Enums.ChiPowerType)(i + 1))
                                        client.ChiPowers.Add(power);
                                }
                            }
                        }
                        data = rdr.ReadBlob("rchipowers");
                        if (data.Length > 0)
                        {
                            using (var stream = new MemoryStream(data))
                            using (var reader = new BinaryReader(stream))
                            {
                                int count = reader.ReadByte();
                                for (int i = 0; i < count; i++)
                                {
                                    var power = new ChiPowerStructure().Deserialize(reader, true);
                                    //  if (power.Power == (Enums.ChiPowerType)(i + 1))
                                    client.Retretead_ChiPowers.Add(power);
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var command = new MySqlCommand(MySqlCommandType.INSERT))
                        {
                            command.Insert("chi").Insert("uid", client.Entity.UID).Insert("name", client.Entity.Name);
                            command.Execute();
                        }
                        client.ChiPoints = 0;
                    }
                }
                client.ChiData = (AllData[client.Entity.UID] = new ChiData() { UID = client.Entity.UID, Name = client.Entity.Name, Powers = client.ChiPowers });
                Sort();
            }
        }
        public static void Save(Client.GameState client)
        {
            if (client == null || client.Entity == null || client.ItemSyncRoot == null) return;
            lock (client.ItemSyncRoot)
            try { SaveCore(client); }
            catch (Exception ex) { Revora.Game.Features.QuestInventory.Fail(client, "chi-save", ex); }
        }
        private static void SaveCore(Client.GameState client)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((byte)client.ChiPowers.Count);
            foreach (var chiPower in client.ChiPowers)
                chiPower.Serialize(writer);
            ///////////
            MemoryStream stream2 = new MemoryStream();
            BinaryWriter writer2 = new BinaryWriter(stream2);
            var count = (byte)client.Retretead_ChiPowers.Count(p => p != null);
            writer2.Write((byte)client.Retretead_ChiPowers.Count(p => p != null));
            foreach (var chiPower in client.Retretead_ChiPowers)
            {
                if (chiPower == null) continue;
                chiPower.Serialize(writer2, true);
            }
            ///////////
            byte[] rawData = stream.ToArray();
            byte[] rawData2 = stream2.ToArray();
            Revora.Game.Features.ChiWallet.Save(client, rawData, rawData2);
        }


        public static object SyncRoot = new object();
        public static void Sort(Enums.ChiPowerType type = Enums.ChiPowerType.None)
        {
            lock (SyncRoot)
            {
                if (type == Enums.ChiPowerType.Dragon || type == Enums.ChiPowerType.None)
                {
                    foreach (var data in AllData.Values) data.DragonRank = 0;
                    Dragon = AllData.Values.Where(c => c.Powers != null && c.Powers.Count >= 1).OrderByDescending((c) => c.DragonPoints).ThenByDescending((c) => c.UID).ToArray();
                    SetRank(Dragon, (a, b) => { a.DragonRank = b; });
                }
                if (type == Enums.ChiPowerType.Phoenix || type == Enums.ChiPowerType.None)
                {
                    foreach (var data in AllData.Values) data.PhoenixRank = 0;
                    Phoenix = AllData.Values.Where(c => c.Powers != null && c.Powers.Count >= 2).OrderByDescending((c) => c.PhoenixPoints).ThenByDescending((c) => c.UID).ToArray();
                    SetRank(Phoenix, (a, b) => { a.PhoenixRank = b; });
                }
                if (type == Enums.ChiPowerType.Tiger || type == Enums.ChiPowerType.None)
                {
                    foreach (var data in AllData.Values) data.TigerRank = 0;
                    Tiger = AllData.Values.Where(c => c.Powers != null && c.Powers.Count >= 3).OrderByDescending((c) => c.TigerPoints).ThenByDescending((c) => c.UID).ToArray();
                    SetRank(Tiger, (a, b) => { a.TigerRank = b; });
                }
                if (type == Enums.ChiPowerType.Turtle || type == Enums.ChiPowerType.None)
                {
                    foreach (var data in AllData.Values) data.TurtleRank = 0;
                    Turtle = AllData.Values.Where(c => c.Powers != null && c.Powers.Count >= 4).OrderByDescending((c) => c.TurtlePoints).ThenByDescending((c) => c.UID).ToArray();
                    SetRank(Turtle, (a, b) => { a.TurtleRank = b; });
                }
            }
        }
        public static void SetRank(ChiData[] array, Action<ChiData, int> modify)
        {
            for (int i = 0; i < array.Length; i++)
                modify(array[i], i + 1);
        }
    }
}
