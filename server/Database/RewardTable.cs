// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class BaisReward
    {
        public struct Reward
        {
            public byte Type;
            public uint ID;
            public uint number;
            public uint NeedPoints;
            public uint GiveReward;
        }
        public static void Load()
        {
            Console.WriteLine("Loading RewardEvent information!");
            string[] text = File.ReadAllLines(Constants.RewardPath);
            for (int x = 0; x < text.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                String line = text[x];
                String[] split = line.Split('#');
                Reward re = new Reward();
                re.ID = uint.Parse(split[0]);
                re.Type = byte.Parse(split[1]);
                re.number = uint.Parse(split[2]);
                re.NeedPoints = uint.Parse(split[3]);
                re.GiveReward = uint.Parse(split[4]);
                RewardBase.Add((uint)x + 1, re);
            }
            //Console.WriteLine("Done!");
        }
        public static Dictionary<uint, Reward> RewardBase = new Dictionary<uint, Reward>(1000);
    }
    public unsafe class RewardDataBase
    {
        private static bool Exists(uint id)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT)
            .Select("Reward").Where("EntityID", id))
            using (var reader = new Database.MySqlReader(cmd))
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
        public static void Load(Client.GameState Client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("Reward").Where("EntityID", Client.Entity.UID))
            using (var r = new MySqlReader(cmd))
            {
                while (r.Read())
                {
                    Client.Entity.Quest_Reward_Clamied = r.ReadUInt16("Quest_Reward_Clamied");
                    Client.Entity.Quest_Reward_Point = r.ReadUInt32("Quest_Reward_Point");
                    Client.Entity.Credit_Reward_Clamied = r.ReadUInt16("Credit_Reward_Clamied");
                    Client.Entity.Credit_Reward_Point = r.ReadUInt32("Credit_Reward_Point");
                    Client.Entity.Online_Reward_Clamied = r.ReadUInt16("Online_Reward_Clamied");
                    Client.Entity.Online_Reward_Point = r.ReadUInt32("Online_Reward_Point");
                    Client.Entity.Kill_Reward_Clamied = r.ReadUInt16("Kill_Reward_Clamied");
                    Client.Entity.Kill_Reward_Point = r.ReadUInt32("Kill_Reward_Point");
                    Client.Entity.King_Reward_Clamied = r.ReadUInt16("King_Reward_Clamied");
                    Client.Entity.King_Reward_Point = r.ReadUInt32("King_Reward_Point");
                    Client.Entity.Arena_Reward_Clamied = r.ReadUInt16("Arena_Reward_Clamied");
                    Client.Entity.Arena_Reward_Point = r.ReadUInt32("Arena_Reward_Point");
                    RewardClameID.Load(Client);
                }
                Insert(Client);
            }
        }
        public static void Save(Client.GameState Client)
        {
            if (Client.Entity == null) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("Reward")
                    .Set("Quest_Reward_Clamied", Client.Entity.Quest_Reward_Clamied)
                    .Set("Quest_Reward_Point", Client.Entity.Quest_Reward_Point)
                    .Set("Credit_Reward_Clamied", Client.Entity.Credit_Reward_Clamied)
                    .Set("Credit_Reward_Point", Client.Entity.Credit_Reward_Point)
                    .Set("Online_Reward_Clamied", Client.Entity.Online_Reward_Clamied)
                    .Set("Online_Reward_Point", Client.Entity.Online_Reward_Point)
                    .Set("Kill_Reward_Clamied", Client.Entity.Kill_Reward_Clamied)
                    .Set("Kill_Reward_Point", Client.Entity.Kill_Reward_Point)
                    .Set("King_Reward_Clamied", Client.Entity.King_Reward_Clamied)
                    .Set("King_Reward_Clamied", Client.Entity.King_Reward_Clamied)
                    .Set("Arena_Reward_Clamied", Client.Entity.Arena_Reward_Clamied)
                    .Set("Arena_Reward_Clamied", Client.Entity.Arena_Reward_Clamied)
                    .Where("EntityID", Client.Entity.UID).Execute();
        }
        public static void Insert(Client.GameState client)
        {
            if (!Exists(client.Entity.UID))
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                    cmd.Insert("Reward")
                     .Insert("EntityID", client.Entity.UID)
                     .Insert("Name", client.Entity.Name)
                     .Execute();
            }
        }
    }
    public unsafe class RewardClameID
    {
        public static void Load(Client.GameState client)
        {
            if (client.Entity != null)
            {
                MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("RewardClameid").Where("EntityID", client.Entity.UID));
                while (reader.Read())
                {
                    uint ClamiedID = reader.ReadUInt32("ClamiedID");
                    uint ClamiedReward = reader.ReadUInt32("ClamiedReward");
                    if (!client.ClamiedReward.ContainsKey(ClamiedID))
                    {
                        client.ClamiedReward.Add(ClamiedID, ClamiedReward);
                    }
                }
                reader.Close();
                reader.Dispose();
            }
        }
        public static void InsertNewClam(Client.GameState client, uint ClamiedID, uint ClamiedReward)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("RewardClameid")
                    .Insert("ClamiedID", ClamiedID)
                    .Insert("ClamiedReward", ClamiedReward)
                    .Insert("EntityID", client.Entity.UID)
                    .Execute();
        }
    }
}