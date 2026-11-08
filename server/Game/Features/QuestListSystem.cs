using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    /// <summary>
    /// Wires Revora quests into the 6711 Quest List GUI (packets 1134/1135).
    /// New player quests must call MarkAccepted / MarkProgress / MarkCompleted / MarkAvailable.
    /// Names and rewards on the client come from ini/Questinfo.ini MissionId entries.
    /// </summary>
    public static class QuestListSystem
    {
        public const uint PathHunt = 90001;
        public const uint CourierLoop = 90002;
        public const uint SoulTrial = 90003;
        public const uint DailyEnvoy = 90004;

        private static readonly uint[] Catalog = { PathHunt, CourierLoop, SoulTrial, DailyEnvoy, ReleaseSoulsQuest.Mission, SkyPassQuest.Mission, 6472, 6473, 6474, 6475 };
        private static bool catalogReady;

        public static void EnsureCatalog()
        {
            if (catalogReady)
                return;
            catalogReady = true;
            QuestInfo.Register((QuestID)PathHunt, QuestType.RegionQuests);
            QuestInfo.Register((QuestID)CourierLoop, QuestType.RegionQuests);
            QuestInfo.Register((QuestID)SoulTrial, QuestType.RegionQuests);
            QuestInfo.Register((QuestID)DailyEnvoy, QuestType.DailyQuest);
            QuestInfo.Register((QuestID)ReleaseSoulsQuest.Mission, QuestType.DailyQuest);
            QuestInfo.Register((QuestID)SkyPassQuest.Mission, QuestType.DailyQuest);
            for(uint mission=6472;mission<=6475;mission++)QuestInfo.Register((QuestID)mission, QuestType.RegionQuests);
        }

        public static void OnLogin(GameState client)
        {
            EnsureCatalog();
            PathHuntSystem.SyncQuestList(client);
            CourierLoopSystem.SyncQuestList(client);
            SoulTrialSystem.SyncQuestList(client);
            DailyQuestEnvoySystem.SyncQuestList(client);
        }

        public static void MarkAccepted(GameState client, uint missionId)
        {
            if (client == null || client.Quests == null)
                return;
            QuestID id = (QuestID)missionId;
            if (client.Quests.HasQuest(id) && client.Quests.CheckQuest(id) == MsgTaskStatus.QuestData.QuestStatus.Finished)
                client.Quests.ClearQuest(id);
            if (!client.Quests.HasQuest(id))
                client.Quests.Accept(id, 0);
        }

        public static void MarkProgress(GameState client, uint missionId, uint a, uint b = 0, uint c = 0, uint d = 0)
        {
            if (client == null || client.Quests == null)
                return;
            QuestID id = (QuestID)missionId;
            if (!client.Quests.HasQuest(id) || client.Quests.CheckQuest(id) != MsgTaskStatus.QuestData.QuestStatus.Accepted)
                MarkAccepted(client, missionId);
            MsgTaskDetailInfo packet = new MsgTaskDetailInfo(true);
            packet.UID = id;
            packet[1] = a;
            packet[2] = b;
            packet[3] = c;
            packet[4] = d;
            client.Send(packet);
        }

        public static void MarkCompleted(GameState client, uint missionId)
        {
            if (client == null || client.Quests == null)
                return;
            QuestID id = (QuestID)missionId;
            if (!client.Quests.HasQuest(id))
                client.Quests.Accept(id, 0);
            client.Quests.FinishQuest(id);
        }

        public static void MarkAvailable(GameState client, uint missionId)
        {
            if (client == null || client.Quests == null)
                return;
            client.Quests.ClearQuest((QuestID)missionId);
        }

        public static void Fill(GameState client, System.Collections.Generic.List<MsgTaskStatus.QuestData> list)
        {
            if (list == null)
                return;
            System.Collections.Generic.HashSet<uint> seen = new System.Collections.Generic.HashSet<uint>();
            AbyssTrialJournal.Fill(client, list, seen);
            MonkEpicJournal.Fill(client, list, seen);
            TaoistEpicJournal.Fill(client, list, seen);
            PirateEpicJournal.Fill(client, list, seen);
            WarriorEpicJournal.Fill(client, list, seen);
            AzurePillQuest.Fill(client, list, seen);
            PowerBookJournal.Fill(client, list, seen);
            ConnectorQuest.Fill(client, list, seen);
            SpunGoldQuest.Fill(client, list, seen);
            ArtisanFeatherQuest.Fill(client, list, seen);
            MysticRingQuest.Fill(client, list, seen);
            FranticMonkeysQuest.Fill(client, list, seen);
            SoulGeneralQuest.Fill(client, list, seen);
            MadManQuest.Fill(client, list, seen);
            GhostCityQuest.Fill(client, list, seen);
            NewcomerIntroduction.Fill(client, list, seen);
            TomboyQuest.Fill(client, list, seen);
            DesertTreasureQuest.Fill(client, list, seen);
            DefenseTalismanQuest.Fill(client, list, seen);
            AttackTalismanQuest.Fill(client, list, seen);
            TempestWingsQuest.Fill(client, list, seen);
            DesertExpeditionQuest.Fill(client, list, seen);
            FamilyFeudQuest.Fill(client, list, seen);
            HeroWingsQuest.Fill(client, list, seen);
            DesertBloodQuest.Fill(client, list, seen);
            TrojanEpicJournal.Fill(client, list, seen);
            for (int i = 0; i < Catalog.Length; i++)
            {
                uint id = Catalog[i];
                list.Add(MsgTaskStatus.QuestData.Create((QuestID)id, StatusOf(client, id), 0));
                seen.Add(id);
            }
            if (client == null || client.Quests == null)
                return;
            foreach (Quests.QuestItem item in client.Quests.Items)
            {
                if (item == null || item.QItem.UID == 0)
                    continue;
                uint id = (uint)item.QItem.UID;
                if (seen.Contains(id))
                    continue;
                if (item.QItem.Status != MsgTaskStatus.QuestData.QuestStatus.Accepted)
                    continue;
                list.Add(item.QItem);
            }
        }

        private static MsgTaskStatus.QuestData.QuestStatus StatusOf(GameState client, uint missionId)
        {
            if (client == null || client.Quests == null || !client.Quests.HasQuest((QuestID)missionId))
                return MsgTaskStatus.QuestData.QuestStatus.Available;
            return client.Quests.CheckQuest((QuestID)missionId);
        }
    }
}
