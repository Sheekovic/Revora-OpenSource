// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using System.Collections.Generic;

namespace Revora.Database
{
    unsafe internal class PrizeNpcTable
    {
        public struct PrizeNpcInfo
        {
            public long Owner;
            public uint type;
            public uint amount;
            public uint itemid;
        }
        public static void Load()
        {
            Console.WriteLine("Loading PrizeNpc information!");
            MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("prizenpc"));
            if (reader.Read())
            {
                PrizeNpcInfo info = new PrizeNpcInfo
                {
                    Owner = reader.ReadUInt32("Owner"),
                    type = reader.ReadUInt32("type"),
                    amount = reader.ReadUInt32("amount"),
                    itemid = reader.ReadUInt32("itemid")
                };
                PrizeNpcInformations.Add(info.Owner, info);
            }
            //Console.WriteLine("Done!");
        }
        public static void RemoveCps(GameState client)
        {
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("prizenpc", "Owner", client.Entity.UID).And("type", "1").Execute();
            PrizeNpcInformations.Remove(client.Entity.UID);
            MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("prizenpc").Where("Owner", client.Entity.UID));
            if (reader.Read())
            {
                PrizeNpcInfo info = new PrizeNpcInfo
                {
                    Owner = reader.ReadUInt32("Owner"),
                    type = reader.ReadUInt32("type"),
                    amount = reader.ReadUInt32("amount"),
                    itemid = reader.ReadUInt32("itemid")
                };
                PrizeNpcInformations.Add(info.Owner, info);
            }
        }
        public static void RemoveItem(GameState client)
        {
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("prizenpc", "Owner", client.Entity.UID).And("type", "2").Execute();
            PrizeNpcInformations.Remove(client.Entity.UID);
            MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("prizenpc").Where("Owner", (long)client.Entity.UID));
            if (reader.Read())
            {
                PrizeNpcInfo info = new PrizeNpcInfo
                {
                    Owner = reader.ReadUInt32("Owner"),
                    type = reader.ReadUInt32("type"),
                    amount = reader.ReadUInt32("amount"),
                    itemid = reader.ReadUInt32("itemid")
                };
                PrizeNpcInformations.Add(info.Owner, info);
            }
        }
        public static void AddCps(GameState client, uint Amount)
        {
            PrizeNpcInfo info = new PrizeNpcInfo
            {
                Owner = client.Entity.UID,
                type = 1,
                amount = Amount,
                itemid = 0
            };
            PrizeNpcInformations.Add(info.Owner, info);
        }
        public static void AddItem(GameState client, uint itemid)
        {
            PrizeNpcInfo info = new PrizeNpcInfo
            {
                Owner = client.Entity.UID,
                type = 2,
                amount = 0,
                itemid = itemid
            };
            PrizeNpcInformations.Add(info.Owner, info);
        }
        public static SafeDictionary<long, PrizeNpcInfo> PrizeNpcInformations = new SafeDictionary<long, PrizeNpcInfo>(50000);
    }
}