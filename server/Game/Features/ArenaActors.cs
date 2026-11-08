using System;
using System.Collections.Concurrent;
using Revora.Client;

namespace Revora.Game.Features
{
    // Only this roster may extend solo-Arena ownership. QuestInventory remains human-only.
    internal static class ArenaActors
    {
        private static readonly ConcurrentDictionary<uint, GameState> bots = new ConcurrentDictionary<uint, GameState>();
        internal static bool Registered(GameState p)
        {
            GameState owned;
            return p!=null && p.Fake && p.Entity!=null && bots.TryGetValue(p.Entity.UID,out owned) && ReferenceEquals(owned,p);
        }
        internal static bool IsBot(GameState p)
        {
            GameState canonical;
            return Registered(p) && Kernel.GamePool.TryGetValue(p.Entity.UID,out canonical) && ReferenceEquals(canonical,p);
        }
        internal static void DetachSettled(GameState p)
        {
            var group=p.QualifierGroup;
            if(!IsBot(p) || group==null || (!ArenaCompletionPump.ResultReady(group) && !group.DurableAdmissionCancelled))
                throw new InvalidOperationException("Cannot detach an unsettled bot.");
            SessionInstall.TryRemoveFake(p.Entity.UID,p);
            p.QualifierGroup=null;p.ArenaStatistic.PlayWith=null;
        }
        internal static bool Player(GameState p, bool alive = true)
        {
            return QuestInventory.Player(p, alive) || IsBot(p) && p.Action == 2 && p.Entity.FullyLoaded
                && !p.Entity.PersistenceRequiresReload && (!alive || !p.Entity.Dead)
                && p.ItemSyncRoot != null && p.Inventory != null;
        }
        internal static bool Install(GameState p)
        {
            if (p == null || !p.Fake || p.Entity == null || !ArenaBotStore.Reserved(p.Entity.UID)) return false;
            if (!bots.TryAdd(p.Entity.UID, p)) return false;
            if (SessionInstall.TryInstallFake(p.Entity.UID, p)) return true;
            GameState ignored; bots.TryRemove(p.Entity.UID, out ignored); return false;
        }
        internal static void Remove(GameState p)
        {
            if (!Registered(p) || p.QualifierGroup != null || p.ArenaShutdownRetention != null)
                throw new InvalidOperationException("Arena bot still owns match work.");
            SessionInstall.TryRemoveFake(p.Entity.UID, p);
            GameState ignored; bots.TryRemove(p.Entity.UID, out ignored);
        }
    }
}
