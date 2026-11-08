// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;

namespace Revora.Database
{
    public unsafe class ActivenessTable
    {
        public static void Load(GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("Activeness").Where("EntityID", client.Entity.UID))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    client.Entity.ActivenessPoint = reader.ReadUInt32("Point");
                    client.Entity.HourTime = reader.ReadByte("HourTime");
                    client.Entity.LotteryTime = reader.ReadByte("LotteryTime");
                    client.Entity.DailyTime = reader.ReadByte("DailyTime");
                    client.Entity.FlowersGiftsTime = reader.ReadByte("FlowersGiftsTime");
                    client.Entity.QualifierTime = reader.ReadByte("QualifierTime");
                    client.Entity.ChampionTime = reader.ReadByte("ChampionTime");
                    client.Entity.HorseRacingTime = reader.ReadByte("HorseRacingTime");
                    client.Entity.TreasureTime = reader.ReadByte("TreasureTime");
                    client.Entity.EnlightenmentTime = reader.ReadByte("EnlightenmentTime");
                    client.Entity.TeamQualifierTime = reader.ReadByte("TeamQualifierTime");
                    client.Entity.ChiStudyTime = reader.ReadByte("ChiStudyTime");
                    client.Entity.JiangHuTime = reader.ReadByte("JiangHuTime");
                    client.Entity.ActivenessDone = reader.ReadBoolean("ActivenessDone");
                    return;
                }
                ActivenessTable.Insert(client);
            }
        }
        public static void Save(GameState client)
        {
            if (client.Entity == null) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Activeness"))
                cmd.Set("Point", client.Entity.ActivenessPoint)
                    .Set("HourTime", client.Entity.HourTime)
                    .Set("LotteryTime", client.Entity.LotteryTime)
                    .Set("DailyTime", client.Entity.DailyTime)
                    .Set("FlowersGiftsTime", client.Entity.FlowersGiftsTime)
                    .Set("QualifierTime", client.Entity.QualifierTime)
                    .Set("ChampionTime", client.Entity.ChampionTime)
                    .Set("HorseRacingTime", client.Entity.HorseRacingTime)
                    .Set("TreasureTime", client.Entity.TreasureTime)
                    .Set("EnlightenmentTime", client.Entity.EnlightenmentTime)
                    .Set("TeamQualifierTime", client.Entity.TeamQualifierTime)
                    .Set("ChiStudyTime", client.Entity.ChiStudyTime)
                    .Set("JiangHuTime", client.Entity.JiangHuTime)
                    .Set("ActivenessDone", client.Entity.ActivenessDone)
                    .Where("EntityID", client.Entity.UID)
                    .Execute();
        }
        public static void Reset(GameState client)
        {
            if (client.Entity == null) return;
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("Activeness"))
             cmd.Set("Point", 5)
                .Set("HourTime", 0)
                .Set("LotteryTime", 0)
                .Set("DailyTime", 0)
                .Set("FlowersGiftsTime", 0)
                .Set("QualifierTime", 0)
                .Set("ChampionTime", 0)
                .Set("HorseRacingTime", 0)
                .Set("TreasureTime", 0)
                .Set("EnlightenmentTime", 0)
                .Set("TeamQualifierTime", 0)
                .Set("ChiStudyTime", 0)
                .Set("JiangHuTime", 0)
                .Set("ActivenessDone", 0)
                .Where("EntityID", client.Entity.UID)
                .Execute();
        }
        private static void Insert(GameState client)
        {
            if (!ActivenessTable.Exists(client.Entity.UID))
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("Activeness"))
                    cmd.Insert("EntityID", client.Entity.UID)
                    .Insert("Point", 0)
                    .Insert("HourTime", 0)
                    .Insert("LotteryTime", 0)
                    .Insert("DailyTime", 0)
                    .Insert("FlowersGiftsTime", 0)
                    .Insert("QualifierTime", 0)
                    .Insert("ChampionTime", 0)
                    .Insert("HorseRacingTime", 0)
                    .Insert("TreasureTime", 0)
                    .Insert("EnlightenmentTime", 0)
                    .Insert("TeamQualifierTime", 0)
                    .Insert("ChiStudyTime", 0)
                    .Insert("JiangHuTime", 0)
                    .Insert("ActivenessDone", 0)
                    .Execute();
            }
        }
        private static bool Exists(uint UID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("Activeness").Where("EntityID", UID))
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
    }
}