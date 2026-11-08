using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class DailyQuestEnvoySystem
    {
        public const uint NpcId = 79882;
        public const string NpcName = "DailyQuestEnvoy";

        public static void Create()
        {
            Console.WriteLine(NpcName + " at Twin City " + Config.DailyEnvoyX + "," + Config.DailyEnvoyY);
        }

        public static void SyncQuestList(GameState client)
        {
            if (client == null || client.Entity == null)
                return;
            if (client.Entity.EvreyThink)
                QuestListSystem.MarkCompleted(client, QuestListSystem.DailyEnvoy);
            else if (client.Entity.PriceQuestID > 0)
                QuestListSystem.MarkAccepted(client, QuestListSystem.DailyEnvoy);
            else
                QuestListSystem.MarkAvailable(client, QuestListSystem.DailyEnvoy);
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            // Old clients may replay the retired paid-reset option. Daily ledgers own
            // eligibility now; clearing legacy flags neither resets nor purchases a run.
            if (request.OptionID == 18)
            {
                if (QuestInventory.Near(client, NpcId, 1002) && (request.NpcID == 0 || request.NpcID == NpcId))
                {
                    var retired = new Npcs(client);
                    retired.Avatar(Config.DailyEnvoyAvatar);
                    retired.Text("Daily tasks renew each day. I cannot reset them.");
                    retired.Option("Farewell.", 255);
                    retired.Send();
                }
                return true;
            }
            if (AncestorWorship.HandleEnvoy(client, request)) return true;
            if (DailyPriceQuest.HandleNpc(client, request)) return true;
            if (DailyRareQuest.HandleNpc(client, request)) return true;
            if (DailySoulBall.HandleNpc(client, request)) return true;
            if (request.OptionID == 0 || request.OptionID == 27) AncestorWorship.Refresh(client);
            if (request.OptionID == 0 || request.OptionID == 27) { DailyPriceQuest.Refresh(client); DailyRareQuest.Refresh(client); DailySoulBall.Refresh(client); AnniversaryQuest.Refresh(client); ExorcismQuest.Refresh(client); }
            if (request.OptionID == 0 || request.OptionID == 25 || request.OptionID == 27) MagnoliaQuest.Refresh(client);
            ReleaseSoulsQuest.Refresh(client);
            if (client.Entity.PersistenceRequiresReload) return true;
            Npcs dialog = new Npcs(client);
            dialog.Avatar(Config.DailyEnvoyAvatar);
            switch (request.OptionID)
            {
                case 0:
                case 27:
                    ShowMenu(client, dialog);
                    return true;
                case 3:
                    TryTeleport(client, dialog, Config.DailySoldiersMinLevel, Config.DailySoldiersMap, Config.DailySoldiersX, Config.DailySoldiersY);
                    return true;
                case 5:
                    ExplorationWorld.Garden(client);
                    return true;
                case 6:
                    TryTeleport(client, dialog, Config.DailyExorcismMinLevel, Config.DailyExorcismMap, Config.DailyExorcismX, Config.DailyExorcismY);
                    return true;
                case 16:
                    ShowParadise(client, dialog);
                    return true;
                case 24:
                    dialog.Text("Help the Desert Guardian release the souls of " + Config.DailyDesertSouls + " soldiers in the desert.");
                    dialog.Text("The Guardian will explain the gifts promised for your hunt.");
                    if (!client.Entity.RelasesTheSoul)
                        dialog.Option("Go to Cloud City.", 3);
                    else
                        dialog.Option("Other tasks.", 27);
                    dialog.Option("Exit.", 255);
                    dialog.Send();
                    return true;
                case 25:
                    dialog.Text("Claim a Magnolia seed and plant it in the garden flower bed.");
                    dialog.Text("Flowered Orchid will show your seed and harvest rewards.");
                    if (!client.Entity.MagniloQuest)
                        dialog.Option("Visit Flowered Orchid.", 5);
                    else
                        dialog.Option("Other tasks.", 27);
                    dialog.Option("Exit.", 255);
                    dialog.Send();
                    return true;
                case 26:
                    dialog.Text("Help the Heavenly Master and Earth Master kill a Thrilling Spook. The stronger the Spook, the more Exorcism Tokens.");
                    dialog.Text("The Exorcist will explain the rewards for your chosen trial.");
                    if (!client.Entity.ThirllingSpook)
                        dialog.Option("Go help him.", 6);
                    else
                        dialog.Option("Other tasks.", 27);
                    dialog.Option("Exit.", 255);
                    dialog.Send();
                    return true;
                case 40:
                    if (Config.DailyParadiseMap != 0)
                        TryTeleport(client, dialog, Config.DailyParadiseMinLevel, Config.DailyParadiseMap, Config.DailyParadiseX, Config.DailyParadiseY);
                    return true;
            }
            return true;
        }

        private static void ShowMenu(GameState client, Npcs dialog)
        {
            dialog.Text("Talk to me for today's tasks. I can send you to the people who accept them.");
            dialog.Text("For Way of Heroes, finish each of my seven regular quests once. You may take several days.");
            dialog.Option("Worship the Ancestors" + Status(1, client), 23);
            dialog.Option("Everything has a price" + Status(2, client), 2);
            dialog.Option("Search for Soldiers" + Status(3, client), 24);
            dialog.Option("Rare Matters" + Status(7, client), 19);
            if (Config.DailyEnvoyShowMagnolia)
                dialog.Option("Flowering Orchid" + Status(4, client), 25);
            dialog.Option("Exorcism" + Status(5, client), 26);
            dialog.Option("Soul Ball" + Status(6, client), 7);
            if (Config.DailyParadiseMap != 0)
                dialog.Option("Treasure of Paradise (Lv. " + Config.DailyParadiseMinLevel + ")", 16);
            dialog.Option("Exit.", 255);
            dialog.Send();
        }

        private static string Status(byte type, GameState client)
        {
            switch (type)
            {
                case 1:
                    return client.Entity.HoringThe ? " (Done)" : " (Lv. " + Config.DailyWorshipMinLevel + ")";
                case 2:
                    if (client.Entity.EvreyThink) return " (Done)";
                    if (client.Entity.PriceQuestID > 0) return " (Accepted)";
                    return " (Lv. " + Config.DailyPriceMinLevel + ")";
                case 3:
                    if (client.Entity.RelasesTheSoul) return " (Done)";
                    if (client.Entity.RelasesGuard > 0) return " (Accepted)";
                    return " (Lv. " + Config.DailySoldiersMinLevel + ")";
                case 4:
                    return client.Entity.MagniloQuest ? " (Done)" : " (Lv. " + Config.DailyMagnoliaMinLevel + ")";
                case 5:
                    if (client.Entity.ThirllingSpook) return " (Done)";
                    if (client.Entity.ThirlSpookLevel > 0) return " (Accepted)";
                    return " (Lv. " + Config.DailyExorcismMinLevel + ")";
                case 6:
                    if (DailySoulBall.CompletedToday(client)) return " (Done)";
                    if (client.Inventory.Objects.Any(i => i.ID == Config.DailySpiritBeadNormal || i.ID == Config.DailySpiritBeadRefined || i.ID == Config.DailySpiritBeadUnique || i.ID == Config.DailySpiritBeadElite || i.ID == Config.DailySpiritBeadSuper)) return " (In progress)";
                    return " (Lv. " + Config.DailySoulBallMinLevel + ")";
                case 7:
                    if (client.Entity.RareMaterials) return " (Done)";
                    if (client.Entity.PriceMaterialsID > 0) return " (In progress)";
                    return " (Lv. " + Config.DailyRareMinLevel + ")";
            }
            return "";
        }

        private static void TryTeleport(GameState client, Npcs dialog, byte minLevel, ushort map, ushort x, ushort y)
        {
            if (client.Entity.Level <= minLevel - 1)
            {
                dialog.Text("Come back when you reach level " + minLevel + ".");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            if (map == 0)
                return;
            client.Entity.PrevX = client.Entity.X;
            client.Entity.PrevY = client.Entity.Y;
            client.Entity.Teleport(map, x, y);
        }

        private static void ShowParadise(GameState client, Npcs dialog)
        {
            if (client.Entity.Level <= Config.DailyParadiseMinLevel - 1)
            {
                dialog.Text("Come back when you reach level " + Config.DailyParadiseMinLevel + ".");
                dialog.Option("Alright.", 255);
                dialog.Send();
                return;
            }
            dialog.Text("Paradise Treasure holds rare prizes. Reborn or level " + Config.DailyParadiseMinLevel + " may enter.");
            if (Config.DailyParadiseMap != 0)
                dialog.Option("Take me to Paradise Treasure.", 40);
            dialog.Option("Not now.", 255);
            dialog.Send();
        }

    }
}
