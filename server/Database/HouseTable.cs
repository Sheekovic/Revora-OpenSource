// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Client;
using Revora.Database;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class HouseTable
    {
        public static void Load(Client.GameState client)
        {
            if (Game.Features.Housing.Enabled) { Game.Features.Housing.Load(client); return; }
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("house").Where("id", (long)client.Account.EntityID))
                using (var reader = new MySqlReader(cmd))
                {
                    if (reader.Read())
                    {
                        MapsTable.MapInformation information = new MapsTable.MapInformation
                        {
                            ID = ((ushort)client.Entity.UID),
                            BaseID = reader.ReadUInt16("Map"),
                            HouseLevel = reader.ReadUInt32("HouseLevel"),
                            Status = 7,
                            Weather = 0,
                            Owner = client.Entity.UID,
                        };
                        if (information.HouseLevel == 0)
                            information.HouseLevel = 1;
                        MapsTable.MapInformations.Add(information.ID, information);
                        Kernel.Maps.Remove(information.ID);
                        if (!Kernel.Maps.ContainsKey(information.ID))
                            new Map(information.ID, information.BaseID, DMaps.MapPaths[information.BaseID]);
                    }
                    using (var cmd1 = new MySqlCommand(MySqlCommandType.SELECT).Select("house").Where("id", client.Entity.UID))
                    {
                        if (reader.Read())
                        {
                            ushort id = (ushort)client.Entity.UID;
                            if (Kernel.Maps.ContainsKey(id))
                                Kernel.Maps.Remove(id);
                            if (MapsTable.MapInformations.ContainsKey(id))
                                MapsTable.MapInformations.Remove(id);

                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Server.SaveException(exception);
            }
        }
        public static void Create(Client.GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("house")
                    .Insert("id", client.Entity.UID)
                    .Insert("Map", "1098")
                    .Insert("owner", client.Entity.UID)
                    .Insert("HouseLevel", "1")
                    .Insert("Name", client.Entity.Name)
                    .Execute();
            MapsTable.MapInformation information = new MapsTable.MapInformation
            {
                ID = (ushort)client.Entity.UID,
                BaseID = 1098,
                Status = 7,
                Weather = 0,
                Owner = client.Entity.UID,
                HouseLevel = 1
            };
            MapsTable.MapInformations.Add(information.ID, information);
            if (!Kernel.Maps.ContainsKey(information.ID))
            {
                new Map(information.ID, information.BaseID, DMaps.MapPaths[information.BaseID]);
            }
        }
        public static void SaveFurniture(byte[] packet, Client.GameState client)
        {
            try
            {
                NpcSpawn npcs = new NpcSpawn();
                npcs.Deserialize(packet);
                npcs.MapID = client.Entity.MapID;
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                    cmd.Insert("Furniture")
                        .Insert("NameOwner", client.Entity.Name)
                        .Insert("id", npcs.UID)
                        .Insert("mapid", npcs.MapID)
                        .Insert("lookface", npcs.Mesh)
                        .Insert("type", (long)npcs.Type)
                        .Insert("cellx", npcs.X)
                        .Insert("celly", npcs.Y)
                        .Execute();
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
        }
        public static void Upgrade(Client.GameState client, ushort _base, byte level)
        {
            MapsTable.MapInformations.Remove((ushort)client.Entity.UID);
            Kernel.Maps.Remove(((ushort)client.Entity.UID));
            MapsTable.MapInformation information = new MapsTable.MapInformation
            {
                ID = (ushort)client.Entity.UID,
                BaseID = _base,
                Status = 7,
                Weather = 0,
                Owner = client.Entity.UID,
                HouseLevel = level
            };
            MapsTable.MapInformations.Add(information.ID, information);

            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("house").Set("HouseLevel", level)
                .Set("Map", _base).Where("id", client.Entity.UID).Execute();

            if (!Kernel.Maps.ContainsKey(information.ID))
            {
                new Map(information.ID, information.BaseID, DMaps.MapPaths[information.BaseID]);
            }
        }
    }
}
