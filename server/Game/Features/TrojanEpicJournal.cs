using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using S = Revora.Game.Features.TrojanEpicStore;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

namespace Revora.Game.Features
{
    // Enabled by the future world coordinator, after storage initialization.
    internal static class TrojanEpicJournal
    {
        private static readonly uint[] missions = { 3269, 3278, 3275, 3270, 3276, 3271, 3277, 3272, 3273, 3279, 3280, 3274 };
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
            if (stage == S.Stage.None) return Status.Available;
            if (stage == S.Stage.Claimed) return Status.Finished;
            // Final victory retains an accepted last mission until the owned claim.
            int active = Math.Min((int)stage, 12) - 1;
            return index < active ? Status.Finished : index == active ? Status.Accepted : Status.Available;
        }
        private static S.Progress Snapshot(GameState p)
        {
            if (!enabled || !Q.Player(p, false)) return null;
            var s = S.Load(p);
            if (s == null || s.Stage == S.Stage.None && !(p.Entity.Class >= 10 && p.Entity.Class <= 15
                && (p.Entity.Level >= 120 || p.Entity.Reborn > 0))) return null;
            return s;
        }
        internal static void Fill(GameState p, List<MsgTaskStatus.QuestData> list, HashSet<uint> seen)
        {
            if (!enabled) return;
            foreach (uint mission in missions) seen.Add(mission);
            if (!Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) try {
                var s = Snapshot(p); if (s == null) return;
                foreach (uint mission in missions) list.Add(MsgTaskStatus.QuestData.Create((QuestID)mission, StatusOf(s.Stage, mission), 0));
            } catch (Exception ex) { Q.Fail(p, "trojan-epic-journal", ex); }
        }
        internal static MsgTaskDetailInfo[] Details(S.Progress s)
        {
            if (s == null) return new MsgTaskDetailInfo[0];
            var traitor = new MsgTaskDetailInfo(true) { UID = (QuestID)3272 };
            var final = new MsgTaskDetailInfo(true) { UID = (QuestID)3274 };
            traitor[1] = s.Stage > S.Stage.Traitor ? 1u : 0;
            final[1] = s.Stage >= S.Stage.Victory ? 1u : 0;
            return new[] { traitor, final };
        }
        internal static void SendDetails(GameState p)
        {
            if (!enabled || !Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) try {
                foreach (var packet in Details(Snapshot(p))) p.Send(packet);
            } catch (Exception ex) { Q.Fail(p, "trojan-epic-details", ex); }
        }
        internal static void Refresh(GameState p)
        {
            if (enabled && Q.Player(p, false) && p.Quests != null) p.Quests.SendFullGUI();
        }
    }
}
