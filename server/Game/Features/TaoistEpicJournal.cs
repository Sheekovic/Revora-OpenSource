using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

namespace Revora.Game.Features
{
    // Historical trial evidence, independent of item possession and the legacy quest blob.
    internal static class TaoistEpicJournal
    {
        internal const uint Mission = 3418;
        internal sealed class View
        {
            internal bool Attempted, Leaf, Crystal, Panacea;
            internal Status Status { get { return Panacea ? Status.Finished : Attempted ? Status.Accepted : Status.Available; } }
        }
        private static readonly ConditionalWeakTable<GameState, View> views = new ConditionalWeakTable<GameState, View>();
        internal static void Initialize() { QuestInfo.Register((QuestID)Mission, QuestType.Nezha_Feud); }
        internal static bool Protect(uint mission) { return TaoistEpicQuest.Enabled && mission == Mission; }
        internal static void Refresh(GameState p, bool send = true)
        {
            if (!TaoistEpicQuest.Enabled || !Q.Player(p, false)) return;
            lock (p.ItemSyncRoot) try
            {
                if (!Q.Player(p, false)) return;
                var view = new View();
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var q = Q.Command(c, null, @"SELECT count(*)>0,
COALESCE(bool_or(kind IN(1,6) AND product=3006013 AND quantity=1),false),
COALESCE(bool_or(kind=2 AND product=3006014 AND quantity=1),false),
COALESCE(bool_or(kind=3 AND product=3006016 AND quantity=1),false)
FROM revora_taoist_epic_operations WHERE uid=@uid AND kind IN(1,2,3,6)", "uid", (long)p.Entity.UID))
                    using (var r = q.ExecuteReader())
                    {
                        if (!r.Read()) throw new InvalidOperationException("Taoist journal evidence missing.");
                        view.Attempted = r.GetBoolean(0); view.Leaf = r.GetBoolean(1); view.Crystal = r.GetBoolean(2); view.Panacea = r.GetBoolean(3);
                    }
                }
                views.Remove(p); views.Add(p, view);
                if (send && p.Quests != null) p.Quests.SendFullGUI();
            }
            catch (Exception ex) { views.Remove(p); Q.Fail(p, "taoist-journal", ex); }
        }
        internal static View Snapshot(GameState p)
        {
            if (!TaoistEpicQuest.Enabled || !Q.Player(p, false)) return null;
            lock (p.ItemSyncRoot)
            {
                if (!Q.Player(p, false)) return null;
                View view;
                if (!views.TryGetValue(p, out view)) { Refresh(p, false); if (!views.TryGetValue(p, out view)) return null; }
                return view.Attempted || TaoistEpicRules.Eligible(p.Entity.Class, p.Entity.Reborn, p.Entity.Level) ? view : null;
            }
        }
        internal static void Fill(GameState p, List<MsgTaskStatus.QuestData> list, HashSet<uint> seen)
        {
            if (!TaoistEpicQuest.Enabled) return;
            seen.Add(Mission); // Suppress an untrusted legacy entry even for ineligible players.
            var view = Snapshot(p);
            if (view != null) list.Add(MsgTaskStatus.QuestData.Create((QuestID)Mission, view.Status, 0));
        }
        internal static MsgTaskDetailInfo Details(View view)
        {
            if (view == null) return null;
            var packet = new MsgTaskDetailInfo(true) { UID = (QuestID)Mission };
            packet[1] = view.Leaf ? 1u : 0; packet[2] = view.Crystal ? 1u : 0; packet[3] = view.Panacea ? 1u : 0;
            return packet;
        }
        internal static void SendDetails(GameState p)
        {
            var packet = Details(Snapshot(p)); if (packet != null) p.Send(packet);
        }
    }
}
