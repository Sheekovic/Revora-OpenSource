// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Database
{
    public unsafe class KnownPersons
    {
        public static void LoadEnemy(Client.GameState client)
        {
            client.Enemy = new SafeDictionary<uint, Enemy>(50);
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("enemy").Where("entityid", client.Entity.UID))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    Enemy enemy = new Enemy();
                    enemy.ID = reader.ReadUInt32("EnemyID");
                    enemy.Name = reader.ReadString("EnemyName");
                    client.Enemy.Add(enemy.ID, enemy);
                }
            }
        }
        public static void LoadMentor(Client.GameState client)
        {
            lock (client.ItemSyncRoot ?? client)
            lock (Game.Features.MentorExperience.Sync) LoadMentorOwned(client);
        }
        private static void LoadMentorOwned(Client.GameState client)
        {
            client.PrizeExperience = 0; client.PrizePlusStone = 0; client.PrizeHeavenBlessing = 0; client.apprtnum = 0;
            client.Mentor = null; client.AsApprentice = null; client.AmbiguousMentor = false;
            client.Apprentices = new SafeDictionary<uint, Apprentice>(10);
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("apprentice").Where("MentorID", client.Entity.UID))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    Apprentice app = new Apprentice();
                    app.ID = reader.ReadUInt32("ApprenticeID");
                    app.Name = reader.ReadString("ApprenticeName");
                    app.EnroleDate = reader.ReadUInt32("EnroleDate");
                    app.Actual_Experience = reader.ReadUInt64("Actual_Experience");
                    app.Total_Experience = reader.ReadUInt64("Total_Experience");
                    app.Actual_Plus = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Actual_Plus").GetValueOrDefault());
                    app.Total_Plus = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Total_Plus").GetValueOrDefault());
                    app.Actual_HeavenBlessing = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Actual_HeavenBlessing").GetValueOrDefault());
                    app.Total_HeavenBlessing = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Total_HeavenBlessing").GetValueOrDefault());
                    client.PrizeExperience = Math.Min(Game.Features.MentorExperience.VisibleCap,
                        client.PrizeExperience + Math.Min(Game.Features.MentorExperience.VisibleCap, app.Actual_Experience));
                    client.PrizePlusStone = (ushort)Math.Min(ushort.MaxValue, (uint)client.PrizePlusStone + app.Actual_Plus);
                    client.PrizeHeavenBlessing = (ushort)Math.Min(ushort.MaxValue, (uint)client.PrizeHeavenBlessing + app.Actual_HeavenBlessing);
                    client.Apprentices.Add(app.ID, app);
                    client.apprtnum += 1;

                    if (client.PrizeExperience > 50 * 606)
                        client.PrizeExperience = 50 * 606;
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("apprentice").Where("ApprenticeID", client.Entity.UID))
            using (var reader = cmd.CreateReader())
            {
                if (reader.Read())
                {
                    client.Mentor = new Mentor();
                    client.Mentor.ID = reader.ReadUInt32("MentorID");
                    client.Mentor.Name = reader.ReadString("MentorName");
                    client.Mentor.EnroleDate = reader.ReadUInt32("EnroleDate");
                    client.AsApprentice = new Revora.Game.ConquerStructures.Society.Apprentice();
                    client.AsApprentice.ID = client.Entity.UID;
                    client.AsApprentice.Name = client.Entity.Name;
                    client.AsApprentice.EnroleDate = client.Mentor.EnroleDate;
                    client.AsApprentice.Actual_Experience = reader.ReadUInt64("Actual_Experience");
                    client.AsApprentice.Total_Experience = reader.ReadUInt64("Total_Experience");
                    client.AsApprentice.Actual_Plus = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Actual_Plus").GetValueOrDefault());
                    client.AsApprentice.Total_Plus = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Total_Plus").GetValueOrDefault());
                    client.AsApprentice.Actual_HeavenBlessing = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Actual_HeavenBlessing").GetValueOrDefault());
                    client.AsApprentice.Total_HeavenBlessing = Game.Features.MentorExperience.ContributionView(reader.ReadNullableInt64Strict("Total_HeavenBlessing").GetValueOrDefault());
                    client.AmbiguousMentor = reader.Read();
                }
            }
            Game.Features.MentorExperience.RefreshPrize(client);
            Game.Features.MentorStoneClaims.Refresh(client);
            Game.Features.MentorBlessingClaims.Refresh(client);
        }
        public static void LoadPartner(Client.GameState client)
        { Game.Features.TradePartnerships.Reload(client, false); }
        public static void LoaderFriends(Client.GameState client)
        { Game.Features.Friendships.Reload(client, false); }
    

        public static void RemoveEnemy(Client.GameState client, uint enemyID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("enemy", "enemyid", enemyID).And("entityid", client.Entity.UID).Execute();
        }
        public static void RemoveFriend(Client.GameState client, uint friendID)
        { Game.Features.Friendships.Remove(client, friendID); }
        public static void RemovePartner(Client.GameState client, uint partnerID)
        { Game.Features.TradePartnerships.Remove(client, partnerID); }


        public static void AddEnemy(Client.GameState client, Enemy enemy)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("enemy").Insert("entityid", client.Entity.UID).Insert("enemyid", enemy.ID)
                    .Insert("enemyname", enemy.Name).Execute();
        }


        public static void SaveApprenticeInfo(Game.ConquerStructures.Society.Apprentice app)
        {
            // Compatibility no-op: contribution writers now commit deltas in their
            // earning transactions. A delayed snapshot must never restore a claim.
        }
        public static void UpdateMessageOnFriend(uint entityID, uint friendID, string message)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("friends")
                    .Set("Message", message)
                    .Where("EntityID", friendID)
                    .And("FriendID", entityID)
                    .Execute();
        }
    }
}
