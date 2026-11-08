using Revora.Client;
using Revora.Network.GamePackets;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Database
{
    class StorageTable
    {
        public static void Load(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("storage").Where("UID", client.Entity.UID))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())                {
                    
                    client.Entity.winnerctf = reader.ReadBoolean("winnerctf");
                    client.Entity.ctf = reader.ReadBoolean("ctf");
                    return;
                }
                Insert(client);
            }
        }
        private static bool Exists(uint UID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("storage").Where("UID", UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    reader.Close();
                    reader.Dispose();
                    return true;
                }
                return false;
            }
        }
        public static void Insert(Client.GameState client)
        {
            if (!Exists(client.Entity.UID))
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("storage"))
                    cmd.Insert("UID", client.Entity.UID)
                        .Execute();
            }
        }
        public static void Save(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("storage"))
                cmd

                    .Set("winnerctf", client.Entity.winnerctf)
                    .Set("ctf", client.Entity.ctf)
                    .Where("UID", client.Entity.UID)
            .Execute();
        }
        public static void UpdateStorage(Client.GameState client)
        {
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.UPDATE))
            {
                command.Update("storage")
                     .Set("winnerctf", client.Entity.winnerctf)
                    .Set("ctf", client.Entity.ctf)
                    .Where("UID", client.Entity.UID)
                    .Execute();
            }
        }
        public static void CheckTitles(GameState client)
        {
            TitleRequirements.Reconcile(client,false);
        }
    }
}
