using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using R = Revora.Game.Features.AbyssTrialRules;
using S = Revora.Game.Features.AbyssTrialStore;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

namespace Revora.Game.Features
{
    // Client-only overlay: never replace, clear or serialize the old Cup entries.
    internal static class AbyssTrialJournal
    {
        private sealed class View { internal S.Run Run; }
        private static readonly ConditionalWeakTable<GameState, View> views = new ConditionalWeakTable<GameState, View>();
        private static bool enabled;
        internal static void Initialize() {
            S.Initialize();
            for (uint mission = 2414; mission <= 2419; mission++) QuestInfo.Register((QuestID)mission, QuestType.Nezha_Feud);
            enabled = true;
        }
        internal static void Refresh(GameState p, bool send = true) {
            if (!enabled || !Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) try {
                if (!Q.Player(p, false)) return;
                using (var c = DataHolder.MySqlConnection) { c.Open(); views.GetValue(p, _ => new View()).Run = S.Read(c, null, p.Entity.UID); }
                if (send && p.Quests != null) p.Quests.SendFullGUI();
            } catch (Exception ex) { views.Remove(p); Q.Fail(p, "abyss-journal", ex); }
        }
        internal static S.Run Snapshot(GameState p) {
            if (!enabled || !Q.Player(p, false)) return null;
            View view;
            if (!views.TryGetValue(p, out view)) { Refresh(p, false); if (!views.TryGetValue(p, out view)) return null; }
            var r = view.Run;
            return r != null && r.Selected && r.FirstClass == p.Entity.FirstRebornClass && r.FirstLevel == p.Entity.FirstRebornLevel ? r : null;
        }
        internal static bool Selected(GameState p) { return Snapshot(p) != null; }
        internal static bool Protect(GameState p, uint mission) {
            if (!enabled || mission < 2414 || mission > 2419) return false;
            return Selected(p) || !Q.Player(p, false);
        }
        internal static Status StatusOf(S.Run r, uint mission) {
            if (r == null || !r.Selected || mission < 2414 || mission > 2419) return Status.Available;
            var stage = r.Progress.Step;
            switch (mission) {
                case 2414: return stage == R.Stage.Vigor ? Status.Accepted : Status.Finished;
                case 2415: return stage < R.Stage.Grievance ? Status.Available : stage == R.Stage.Grievance && !r.Introduced ? Status.Accepted : Status.Finished;
                case 2416: return stage < R.Stage.Grievance || stage == R.Stage.Grievance && !r.Introduced ? Status.Available : stage == R.Stage.Grievance ? Status.Accepted : Status.Finished;
                case 2417: return stage < R.Stage.Spirits ? Status.Available : stage == R.Stage.Spirits && !r.Introduced ? Status.Accepted : Status.Finished;
                case 2418: return stage < R.Stage.Spirits || stage == R.Stage.Spirits && !r.Introduced ? Status.Available : stage == R.Stage.Spirits ? Status.Accepted : Status.Finished;
                default: return stage < R.Stage.Satan ? Status.Available : stage == R.Stage.Satan ? Status.Accepted : Status.Finished;
            }
        }
        internal static void Fill(GameState p, List<MsgTaskStatus.QuestData> list, HashSet<uint> seen) {
            var r = Snapshot(p); if (r == null) return;
            for (uint mission = 2414; mission <= 2419; mission++) {
                list.Add(MsgTaskStatus.QuestData.Create((QuestID)mission, StatusOf(r, mission), 0)); seen.Add(mission);
            }
        }
        internal static MsgTaskDetailInfo[] Details(S.Run r) {
            if (r == null || !r.Selected) return new MsgTaskDetailInfo[0];
            var grief = new MsgTaskDetailInfo(true) { UID = (QuestID)2416 }; grief[1] = (uint)r.Progress.Meters;
            var spirits = new MsgTaskDetailInfo(true) { UID = (QuestID)2418 };
            spirits[1] = (uint)(r.Progress.Spirits / 8);
            // Keep the just-finished round at eight; the next Andrew starts at one.
            spirits[2] = r.Progress.Spirits == 0 ? 0u : (uint)((r.Progress.Spirits - 1) % 8 + 1);
            return new[] { grief, spirits };
        }
        internal static void SendDetails(GameState p) {
            var r = Snapshot(p); if (r == null) return;
            foreach (var packet in Details(r)) p.Send(packet);
        }
    }
}
