// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class SubClassTable
    {
        public static void Load(Entity Entity)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("subclasses").Where("id", Entity.UID))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    Game.SubClass Sub = new Game.SubClass();
                    Sub.ID = reader.ReadByte("Uid");
                    Sub.Level = reader.ReadByte("Level");
                    Sub.Phase = reader.ReadByte("Phase");
                    Entity.SubClasses.Classes.Add(Sub.ID, Sub);
                    GameSubClass packet = new GameSubClass();
                    packet.ClassId = (GameSubClass.ID)Sub.ID;
                    packet.Phase = Sub.Phase;
                    packet.Type = GameSubClass.Types.Learn;
                    Entity.Owner.Send(packet);
                    packet.Type = GameSubClass.Types.MartialPromoted;
                    Entity.Owner.Send(packet);
                }
            }
        }
        public static void Insert(Entity Entity, byte id)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.INSERT))
                Command.Insert("subclasses").Insert("uid", id).Insert("id", Entity.UID).Execute();
        }
        public static void Study(Client.GameState client)
        {
            EntityTable.UpdateData(client, "StudyPoints", client.Entity.SubClasses.StudyPoints);
        }
        public static bool Contains(Entity Entity, byte id)
        {
            bool Return = false;
            using (var Command = new MySqlCommand(MySqlCommandType.SELECT))
            {
                Command.Select("subclasses").Where("id", Entity.UID).And("uid", id);
                using (var Reader = Command.CreateReader())
                    if (Reader.Read())
                        if (Reader.ReadByte("uid") == id)
                            Return = true;
            }
            return Return;
        }
        public static void Update(Game.Entity Entity, Game.SubClass SubClass)
        {
            using (var Command = new MySqlCommand(MySqlCommandType.UPDATE))
                Command.Update("subclasses")
                .Set("phase", SubClass.Phase)
                .Set("level", SubClass.Level)
                .Where("id", Entity.UID)
                .And("uid", SubClass.ID)
                .Execute();
        }
    }
}