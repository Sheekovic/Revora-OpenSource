using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class StatueTable
    {
        public static void Load()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("statue").Where("UID", 105175))
            using (var reader = cmd.CreateReader())
            {
                if (reader.Read())
                {
                    using (var stream = new MemoryStream((reader.ReadBlob("data"))))
                    using (var hreader = new BinaryReader(stream))
                    {
                        uint Count = hreader.ReadUInt32();
                        byte[] array = new byte[Count];
                        for (int x = 0; x < Count; x++)
                            array[x] = hreader.ReadByte();
                        new Game.Statue(array);
                    }
                }
                else
                {
                    string SQL = "INSERT INTO `statue` (uid, data) VALUES (@UID, @Data)";
                    MemoryStream stream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(stream);
                    writer.Write(0);
                    byte[] rawData = stream.ToArray();
                    using (var conn = DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        using (var cmd2 = new Npgsql.NpgsqlCommand(Revora.Database.DataHolder.ToPostgres(SQL), conn))
                        {
                            DataHolder.AddParameter(cmd2, "@UID", 105175);
                            DataHolder.AddParameter(cmd2, "@Data", rawData);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        public static void Save()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            bool WasAdd = false;
            foreach (var statu in Game.Statue.Statues.Values)
            {
                if (statu.SpawnPacket != null)
                {
                    if (statu.SpawnPacket.Length > 200)
                    {
                        WasAdd = true;
                        writer.Write(statu.SpawnPacket.Length);
                        writer.Write(statu.SpawnPacket);
                    }
                }
            }
            if (!WasAdd)
                writer.Write(0);
            string SQL = "UPDATE `statue` SET data=@Data where UID = " + 105175 + " ;";
            byte[] rawData = stream.ToArray();
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = Revora.Database.DataHolder.ToPostgres(SQL);
                    DataHolder.AddParameter(cmd, "@Data", rawData);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}