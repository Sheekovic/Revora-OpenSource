using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
namespace Revora.Game.Features
{
    internal static class ArenaCacheRecovery
    {
        private sealed class Invalid { }
        private static readonly ConditionalWeakTable<object,Invalid> invalid = new ConditionalWeakTable<object,Invalid>();
        internal static bool RequiresReload(object stats) {
            Invalid marker;return stats!=null && invalid.TryGetValue(stats,out marker);
        }
        private static void Remove<T>(ConcurrentDictionary<uint,T> cache,uint uid,T stats) where T:class {
            if(stats==null)return;
            invalid.GetValue(stats,key=>new Invalid());
            // A late failure from the old session must not remove a replacement's statistic.
            ((ICollection<KeyValuePair<uint,T>>)cache).Remove(new KeyValuePair<uint,T>(uid,stats));
        }
        internal static void Fail(GameState p,string source,Exception ex) {
            // No database access: this must also work during database outages.
            Remove(Qualifier.ArenaStatistics,p.Entity.UID,p.ArenaStatistic);
            Remove(TeamQualifier.ArenaStatistics,p.Entity.UID,p.TeamArenaStatistic);
            QuestInventory.Fail(p,source,ex);
        }
    }
}
