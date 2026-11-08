using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ClassicDances
    {
        private sealed class State { internal uint EarnedOwner; }
        private static readonly ConditionalWeakTable<GameState, State> States = new ConditionalWeakTable<GameState, State>();
        private static bool InCity(GameState p)
        { return p.Entity.MapID == 1002 && p.Map.BaseID == 1002 && TravelRegions.At(1002,p.Entity.X,p.Entity.Y).Contains(10612); }

        private static bool Ready(GameState p, Data request, uint action)
        {
            Map map;
            if (!Q.Player(p) || !ReferenceEquals(p.Entity.Owner, p) || request.UID != p.Entity.UID
                || request.dwParam != action || p.Booth != null || p.Trade == null || p.Trade.InTrade
                || p.Entity.Transformed || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride)
                || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly)
                || !Kernel.Maps.TryGetValue(p.Entity.MapID, out map) || !ReferenceEquals(map, p.Map)) return false;
            if (action == 1) return true;
            ushort id = (ushort)(1380 + (action - 2) * 5);
            Interfaces.ISkill skill;
            return p.Spells != null && p.Spells.TryGetValue(id, out skill) && skill != null
                && skill.ID == id && skill.Available && skill.Level == 0;
        }

        // Own only the eight classic emotes; combat/Performer transformations
        // are distinct actions and do not attest to this requirement here.
        internal static bool TryHandle(GameState p, Data request)
        {
            if (request == null) return false;
            uint action = request.dwParam & 0xffff;
            if (action < 1 || action > 8) return false;
            if (!Ready(p, request, action)) return true;
            lock (p.ItemSyncRoot) try
            {
                if (!Ready(p, request, action)) return true;
                bool added = false;
                var state = States.GetValue(p, _ => new State());
                if (InCity(p) && state.EarnedOwner != p.Entity.UID)
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            SkillTable.LockOwner(c, t, p);
                            if (!Ready(p, request, action) || !InCity(p))
                                throw new InvalidOperationException("Dance participant changed.");
                            added = ProgressionEvidence.Complete(c, t, p.Entity.UID, 10121,
                                "classic-dance-v1", "1002:" + action.ToString(CultureInfo.InvariantCulture), 1);
                            t.Commit();
                        }
                    }
                    state.EarnedOwner = p.Entity.UID;
                }
                p.Entity.Action = (ushort)action;
                request.UID = p.Entity.UID; request.dwParam = action;
                request.wParam1 = p.Entity.X; request.wParam2 = p.Entity.Y;
                p.SendScreen(request, false);
                if (added) Q.Refresh(p);
            }
            catch (Exception ex) { Q.Fail(p, "classic-dance", ex); }
            return true;
        }
    }
}
