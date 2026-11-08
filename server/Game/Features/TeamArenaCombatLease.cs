using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Revora.Game.Features
{
    // Hold the admission owners through health, score and terminal callbacks.
    internal sealed class TeamArenaCombatLease : IDisposable
    {
        private readonly List<object> gates = new List<object>();
        internal bool Allowed { get; private set; }
        internal bool Busy { get; private set; }
        private bool Own(object gate)
        {
            if (gate == null) return false;
            if (!Monitor.TryEnter(gate)) { Busy = true;return false; }
            gates.Add(gate);return true;
        }
        internal static TeamArenaCombatLease Acquire(Entity attacker, Entity target)
        {
            var a = attacker == null ? null : attacker.Owner;
            var b = target == null ? null : target.Owner;
            var first = a == null ? null : a.TeamQualifierGroup;
            var second = b == null ? null : b.TeamQualifierGroup;
            if (first == null && second == null) return null;
            var lease = new TeamArenaCombatLease();
            try
            {
                if (first == null || !ReferenceEquals(first, second) || a.Entity == null || b.Entity == null
                    || !lease.Own(first.ResultSync)) return lease;
                foreach (var p in new[] { a, b }.Distinct().OrderBy(p => p.Entity.UID))
                    if (!lease.Own(p.ItemSyncRoot)) return lease;
                if (!lease.Own(first.DepartureSync)) return lease;
                lease.Allowed = first.AllowsCombat(a, b, TeamArenaResultCoordinator.MatchClock());
                return lease;
            }
            catch { lease.Dispose();throw; }
        }
        public void Dispose()
        {
            for (int n = gates.Count - 1; n >= 0; n--) Monitor.Exit(gates[n]);
            gates.Clear();
        }
    }
}
