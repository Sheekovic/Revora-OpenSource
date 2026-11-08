// * C++ programmer ((Pezzi Tomas))
// * All rights reserved © 2015 - 2016
// * Emulator [ Private Server ] - TQ Project

using System;
using System.IO;
using COServer.Network.GamePackets;

namespace COServer.Database
{
    public unsafe class ClaimItemTable
    {
        public static Counter Counter = new COServer.Counter(1);
        public static void LoadClaimableItems(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("claimitems").Where("GainerUID", client.Entity.UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    DetainedItem item = new DetainedItem(true);
                    item.ItemUID = reader.ReadUInt32("ItemUID");
                    item.UID = item.ItemUID - 1;
                    item.Page = (byte)DetainedItem.ClaimPage;
                    item.Item = ConquerItemTable.LoadItem(item.ItemUID);
                    item.ConquerPointsCost = reader.ReadUInt32("ConquerPointsCost");
                    item.OwnerUID = reader.ReadUInt32("OwnerUID");
                    if (item.Item.Bound)
                    {
                        item.Buffer[39] = 3;
                    }
                    item.GainerName = reader.ReadString("GainerName");
                    item.GainerUID = reader.ReadUInt32("GainerUID");
                    item.OwnerName = reader.ReadString("OwnerName");
                    item.Date = DateTime.FromBinary(reader.ReadInt64("Date"));
                    item.DaysLeft = (uint)(TimeSpan.FromTicks(DateTime.Now.Ticks).Days - TimeSpan.FromTicks(item.Date.Ticks).Days);
                    if (item.OwnerUID == 500)
                    {
                        item.MakeItReadyToClaim();
                        item.GainerUID = reader.ReadUInt32("GainerUID");
                        item.OwnerUID = reader.ReadUInt32("OwnerUID");
                    }
                    if (item.Item.Bound && DateTime.Now >= item.Date.AddDays(7))
                    {
                        Claim(item, client);
                        continue;
                    }
                    client.ClaimableItem.Add(item.UID, item);
                }
            }
        }
        public static void Redeem(DetainedItem item, Client.GameState owner)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("claimitems").Set("OwnerUID", 500).Where("ItemUID", item.ItemUID).Execute();
        }
        public static void Claim(DetainedItem item, Client.GameState owner)
        {
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("claimitems", "ItemUID", item.ItemUID).And("GainerUID", owner.Entity.UID).Execute();
        }
        public static uint CalculateCost(ConquerItem item)
        {
            int basic = 10;
            if (item.ID % 10 == 9)
                basic += 50;
            if (item.ID / 100000 == 4 || item.ID / 100000 == 5)
            {
                if (item.SocketOne != Game.Enums.Gem.NoSocket)
                    basic += 100;
                if (item.SocketTwo != Game.Enums.Gem.NoSocket)
                    basic += 400;
            }
            else
            {
                if (item.SocketOne != Game.Enums.Gem.NoSocket)
                    basic += 400;
                if (item.SocketTwo != Game.Enums.Gem.NoSocket)
                    basic += 1600;
            }
            basic += item.Plus * 100;
            return (uint)basic;
        }
    }
}