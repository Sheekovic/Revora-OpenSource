using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace COServer.Network.GamePackets
{
    public class WardrobeTitles
    {
        public uint Id;
        public byte[] Data;
        public int Points;

        public void Create()
        {
            string SQL = "INSERT INTO `Titles` (Id, Points, Data) VALUES (@Id, @Points ,@Data)";
            using (var conn = Database.DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand(Revora.Database.DataHolder.ToPostgres(SQL), conn))
                {
                    var reader = new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("Titles").Where("Id", Id).CreateReader();
                    if (reader.Read())
                    {
                        Update();
                        return;
                    }
                    Revora.Database.DataHolder.AddParameter(cmd, "@Id", Id);
                    Revora.Database.DataHolder.AddParameter(cmd, "@Points", Points);
                    Revora.Database.DataHolder.AddParameter(cmd, "@Data", Data);
                    cmd.ExecuteNonQuery();
                }
            }
            if (StorageManager.Data.ContainsKey(Id))
                StorageManager.Data[Id] = this;
            else StorageManager.Data.Add(Id, this);
        }
        public void Update()
        {
            string SQL = "UPDATE `Titles` SET Data=@Data where ID = " + Id + " ;";
            using (var conn = Database.DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = Revora.Database.DataHolder.ToPostgres(SQL);
                    Revora.Database.DataHolder.AddParameter(cmd, "@Data", Data);
                    cmd.ExecuteNonQuery();
                }
            }
            if (StorageManager.Data.ContainsKey(Id))
                StorageManager.Data[Id] = this;
            else StorageManager.Data.Add(Id, this);
        }

        public void AddTitle(short _type, short _id, bool equipped = false)
        {
            using (var writer = new BinaryWriter(new MemoryStream()))
            {
                Data[0]++;
                writer.Write(Data);
                writer.Write(_type);
                writer.Write(_id);
                writer.Write(equipped);


                Data = (writer.BaseStream as MemoryStream).ToArray();
            }

            Update();
        }

        public void Update(short _type, short _id, bool equipped)
        {
            using (var reader = new BinaryReader(new MemoryStream(Data)))
            {
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    var type = reader.ReadInt16();
                    var id = reader.ReadInt16();
                    if (type == _type && id == _id)
                    {
                        Data[reader.BaseStream.Position++] = equipped ? (byte)1 : (byte)0;
                    }
                    else if (StorageManager.Wing<bool>(_type, _id) && StorageManager.Wing<bool>(type, id))
                        Data[reader.BaseStream.Position++] = 0;
                    else if (StorageManager.Title<bool>(_type, _id) && StorageManager.Title<bool>(type, id))
                        Data[reader.BaseStream.Position++] = 0;
                    else
                        reader.BaseStream.Position++;
                }
            }
            Update();
        }
    }
}