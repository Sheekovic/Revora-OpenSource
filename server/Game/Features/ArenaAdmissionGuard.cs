using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ArenaAdmissionGuard
    {
        // Activated only with the staged team lifecycle. SQL locks end before teleport/send;
        // canonical owner locks remain held through import to serialize in-process writers.
        internal static bool Run(object matchLock, GameState[] players, Func<GameState, bool> owns, Action import, Action<Npgsql.NpgsqlConnection,Npgsql.NpgsqlTransaction> reserve = null, bool soloBots = false)
        {
            if (players == null || players.Length < 2 || players.Any(p => p == null || p.Entity == null)) return false;
            var owners = players.Distinct().OrderBy(p => p.Entity.UID).ToArray();
            if (owners.Length != players.Length || owners.Select(p => p.Entity.UID).Distinct().Count() != owners.Length) return false;
            if (!Monitor.TryEnter(matchLock)) return false;
            int acquired = 0;
            try
            {
                foreach (var p in owners)
                {
                    if (p.ItemSyncRoot == null || !Monitor.TryEnter(p.ItemSyncRoot)) return false;
                    acquired++;
                }
                if (owners.Any(p => !(Q.Player(p,false) || soloBots && ArenaActors.Player(p,false)) || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID) || !owns(p))) return false;
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            foreach (var p in owners)
                            {
                                SkillTable.LockOwner(c,t,p);
                                if (TeamArenaResultStatistics.Pending(c,t,p.Entity.UID) || ArenaMatchLedger.Pending(c,t,p.Entity.UID)) return false;
                            }
                            if (owners.Any(p => !(Q.Player(p,false) || soloBots && ArenaActors.Player(p,false)) || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID) || !owns(p))) return false;
                            if(reserve!=null)reserve(c,t);
                            if (owners.Any(p => !(Q.Player(p,false) || soloBots && ArenaActors.Player(p,false)) || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID) || !owns(p))) return false;
                            t.Commit();
                        }
                    }
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    Console.WriteLine("Arena admission deferred: " + ex.GetType().Name);
                    return false;
                }
                import();return true;
            }
            finally
            {
                for (int n = acquired - 1; n >= 0; n--) Monitor.Exit(owners[n].ItemSyncRoot);
                Monitor.Exit(matchLock);
            }
        }
    }
}
