using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game.Features.Reincarnation;

namespace Revora.Game.Features
{
    internal static class ReincarnationRecovery
    {
        internal static void Validate(ReincarnateInfo info)
        {
            if (info == null || info.UID == 0 || info.Level < (info.FirstRebirth || info.SecondRebirth ? 130 : 110) || info.Experience > long.MaxValue
                || info.PaidReincarnation && (info.PaidReceipt == Guid.Empty || info.FirstRebirth || info.SecondRebirth))
                throw new InvalidOperationException("Invalid reincarnation snapshot.");
        }
        internal static ReincarnateInfo Snapshot(GameState p)
        {
            if (p.Entity.Reborn == 1) return FirstRebirthRecovery.Snapshot(p);
            ReincarnateInfo info;
            if (p.Entity.Reborn != 2) return null;
            var paid = PaidReincarnationRecovery.Snapshot(p);
            if (paid != null) return paid;
            if (!Kernel.ReincarnatedCharacters.TryGetValue(p.Entity.UID, out info)) return SecondRebirthRecovery.Snapshot(p);
            Validate(info);
            if (info.UID != p.Entity.UID) throw new InvalidOperationException("Reincarnation snapshot owner differs.");
            return new ReincarnateInfo { UID = info.UID, Level = info.Level, Experience = info.Experience };
        }
        internal static void Consume(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ReincarnateInfo restored)
        {
            if (restored == null) return;
            if (t == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || !QuestInventory.Player(p) || restored.UID != p.Entity.UID)
                throw new InvalidOperationException("Reincarnation recovery is not ready.");
            if (restored.FirstRebirth) { FirstRebirthRecovery.Consume(c, t, p, restored); return; }
            if (restored.SecondRebirth) { SecondRebirthRecovery.Consume(c, t, p, restored); return; }
            if (restored.PaidReincarnation) { PaidReincarnationRecovery.Consume(c, t, p, restored); return; }
            if (QuestInventory.Execute(c, t, "DELETE FROM reincarnation WHERE uid=@uid AND level=@level AND experience=@exp",
                "uid", (long)p.Entity.UID, "level", (long)restored.Level, "exp", (long)restored.Experience) != 1)
                throw new InvalidOperationException("Reincarnation snapshot changed.");
        }
        internal static void Committed(GameState p, ReincarnateInfo restored)
        {
            if (restored == null) return;
            if (!Monitor.IsEntered(p.ItemSyncRoot ?? p) || restored.UID != p.Entity.UID)
                throw new InvalidOperationException("Reincarnation recovery owner differs.");
            if (restored.FirstRebirth) { FirstRebirthRecovery.Committed(p, restored); return; }
            if (restored.SecondRebirth) { SecondRebirthRecovery.Committed(p, restored); return; }
            if (restored.PaidReincarnation) { PaidReincarnationRecovery.Committed(p, restored); return; }
            ReincarnateInfo removed;
            Kernel.ReincarnatedCharacters.TryRemove(p.Entity.UID, out removed);
        }
        internal static void RestoreReady(GameState p)
        {
            if (!QuestInventory.Player(p)) return;
            lock (p.ItemSyncRoot)
            {
                if (!QuestInventory.Player(p) || p.Entity.Level < 110) return;
                var info = Snapshot(p);
                if (info != null && !info.Recovered && p.Entity.Level >= (info.FirstRebirth || info.SecondRebirth ? 130 : 110)) p.IncreaseExperience(0, false);
            }
        }
    }
}
