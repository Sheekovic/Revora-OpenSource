using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Revora.Game.Features
{
    internal static class ArenaShutdownDrain
    {
        private static readonly object Gate = new object();
        private static int stopping;
        private static Task<bool> work;
        private static int entries;
        private sealed class Entry : IDisposable
        {
            private int disposed;
            public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) lock (Gate) entries--; }
        }
        internal static IDisposable Enter()
        {
            lock (Gate) { if (Stopping) return null; entries++; return new Entry(); }
        }
        internal static bool Stopping { get { return Volatile.Read(ref stopping) != 0; } }

        internal static bool TryDrain(int milliseconds)
        {
            if (milliseconds < 10 || milliseconds > 30000) throw new ArgumentOutOfRangeException("milliseconds");
            if (!ArenaStartupPreparation.Prepared) return true;
            Task<bool> current;
            lock (Gate)
            {
                Interlocked.Exchange(ref stopping, 1);
                // A timed-out caller must not launch another worker while an
                // earlier database operation is still running.
                if (work == null || work.IsCompleted && !work.Result)
                    work = Task.Run(() => Drain(milliseconds));
                current = work;
            }
            return current.Wait(milliseconds) && current.Result;
        }
        private static bool Drain(int milliseconds)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                do
                {
                    // Existing admissions must finish before queued withdrawals:
                    // otherwise a queued actor could become a fighter mid-drain.
                    bool admitting; lock (Gate) admitting = entries != 0;
                    if (admitting) { System.Threading.Thread.Sleep(10); continue; }
                    foreach (var p in Qualifier.PlayerList.Values.Concat(Qualifier.WaitingPlayerList.Values).Distinct().Take(128))
                        if (p.QualifierGroup == null && p.TeamQualifierGroup == null) Qualifier.SignupWithdrawals.Request(p);
                    foreach (var p in TeamQualifier.PlayerList.Values.Concat(TeamQualifier.WaitingPlayerList.Values).Distinct().Take(128))
                        if (p.QualifierGroup == null && p.TeamQualifierGroup == null) TeamQualifier.SignupWithdrawals.Request(p);
                    foreach (var g in Qualifier.QualifierList.Groups.Values.ToArray())
                        if (g.OwnedEntryRequested && !g.ImportStarted) g.RejectUnimported();
                    foreach (var g in TeamQualifier.QualifierList.Groups.Values.ToArray())
                        if (g.OwnedEntryRequested && !g.ImportStarted) g.RejectUnimported();
                    Qualifier.SignupWithdrawals.Tick(); TeamQualifier.SignupWithdrawals.Tick();
                    ArenaDisconnectRetention.TickScheduled(); TeamArenaDisconnectRetention.TickScheduled();
                    Champion.EngagePlayers();Champion.CheckGroups();
                    if (Drained()) return true;
                    System.Threading.Thread.Sleep(10);
                } while (elapsed.ElapsedMilliseconds < milliseconds);
                return false;
            }
            catch (Exception ex) { Console.WriteLine("[arena-shutdown] error=" + ex.GetType().Name); return false; }
        }
        private static bool Drained()
        {
            return Qualifier.QualifierList.Groups.Count == 0 && TeamQualifier.QualifierList.Groups.Count == 0
                && Champion.QualifierList.Groups.Count==0 && Champion.PlayerList.Count==0 && Champion.WaitingPlayerList.Count==0 && !Champion.HasRetainedShutdown
                && Qualifier.PlayerList.Count == 0 && Qualifier.WaitingPlayerList.Count == 0
                && TeamQualifier.PlayerList.Count == 0 && TeamQualifier.WaitingPlayerList.Count == 0
                && Qualifier.SignupWithdrawals.Count == 0 && TeamQualifier.SignupWithdrawals.Count == 0
                && !ArenaDisconnectRetention.HasScheduled && !TeamArenaDisconnectRetention.HasScheduled
                && !Kernel.GamePool.Values.Concat(Kernel.DisconnectPool.Values).Any(p => p != null
                    && (p.ArenaShutdownRetention != null || p.QualifierGroup != null || p.TeamQualifierGroup != null || p.ChampionGroup!=null
                        || p.WatchingGroup != null || p.TeamWatchingGroup != null));
        }
    }
}
