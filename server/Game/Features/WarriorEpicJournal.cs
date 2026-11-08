using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using S = Revora.Game.Features.WarriorEpicStore;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

namespace Revora.Game.Features
{
    // Initialized with the ordinary-player quest coordinator.
    // Read saved progress on each projection: reconnect and lost post-commit sends
    // must not preserve an obsolete per-session snapshot.
    internal static class WarriorEpicJournal
    {
        private static readonly uint[] missions = { 3678, 3679, 3695, 3680, 3681, 3682, 3683 };
        private static bool enabled;
        internal static void Initialize()
        {
            foreach (uint mission in missions) QuestInfo.Register((QuestID)mission, QuestType.Nezha_Feud);
            enabled = true;
        }
        internal static bool Protect(uint mission) { return enabled && Array.IndexOf(missions, mission) >= 0; }
        internal static Status StatusOf(S.Stage stage, uint mission)
        {
            int index = Array.IndexOf(missions, mission);
            if (index < 0 || stage < S.Stage.None || stage > S.Stage.Claimed) throw new ArgumentOutOfRangeException();
            return (int)stage > index ? Status.Finished : (int)stage == index && stage != S.Stage.None ? Status.Accepted : Status.Available;
        }
        internal static void Refresh(GameState p)
        {
            if (enabled && Q.Player(p, false) && p.Quests != null) p.Quests.SendFullGUI();
        }
        internal static void Fill(GameState p, List<MsgTaskStatus.QuestData> list, HashSet<uint> seen)
        {
            if (!enabled) return;
            // Suppress forged/stale legacy entries even for ineligible sessions.
            foreach (uint mission in missions) seen.Add(mission);
            if (!Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) try
            {
                var progress = S.Load(p);
                if (progress == null || progress.Stage == S.Stage.None &&
                    (p.Entity.Level < 40 || p.Entity.Class < 20 || p.Entity.Class > 25)) return;
                foreach (uint mission in missions)
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)mission, StatusOf(progress.Stage, mission), 0));
            }
            catch (Exception ex) { Q.Fail(p, "warrior-epic-journal", ex); }
        }
    }
}
