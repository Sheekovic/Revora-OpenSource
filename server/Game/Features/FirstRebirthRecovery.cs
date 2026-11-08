using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game.Features.Reincarnation;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class FirstRebirthRecovery
    {
        private sealed class Loaded { internal Entity Entity; internal uint Uid; internal ReincarnateInfo Info; }
        private static readonly ConditionalWeakTable<GameState, Loaded> loaded = new ConditionalWeakTable<GameState, Loaded>();
        internal static ReincarnateInfo Snapshot(GameState p) {
            if (!Monitor.IsEntered(p.ItemSyncRoot ?? p)) throw new InvalidOperationException("First-life recovery requires its owner lock.");
            if (p.Fake || p.Entity.Reborn != 1 || p.Entity.FirstRebornLevel < 130) return null;
            Loaded state;
            if (!loaded.TryGetValue(p, out state) || !ReferenceEquals(state.Entity, p.Entity) || state.Uid != p.Entity.UID) {
                state = new Loaded { Entity = p.Entity, Uid = p.Entity.UID };
                try {
                    using (var c = DataHolder.MySqlConnection) { c.Open();
                        using (var q = Q.Command(c, null, "SELECT old_class,old_level,old_experience,new_class,recovered_at FROM revora_first_rebirths WHERE uid=@uid", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) if (r.Read()) {
                            byte level = checked((byte)Convert.ToInt32(r[1]));
                            byte oldClass = checked((byte)Convert.ToInt32(r[0])), nextClass = checked((byte)Convert.ToInt32(r[3]));
                            if (!RebirthRules.Eligible(oldClass, level, 0) || Array.IndexOf(RebirthRules.Classes, nextClass) < 0
                                || oldClass != p.Entity.FirstRebornClass || level != p.Entity.FirstRebornLevel || nextClass / 10 != p.Entity.Class / 10)
                                throw new InvalidOperationException("First-life receipt differs from its character.");
                            state.Info = new ReincarnateInfo { UID = p.Entity.UID, Level = level, Experience = checked((ulong)Convert.ToInt64(r[2])),
                                FirstRebirth = true, Recovered = !r.IsDBNull(4), RebirthClass = nextClass };
                            ReincarnationRecovery.Validate(state.Info);
                        }
                        if (state.Info == null) {
                            using (var q = Q.Command(c, null, "SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND source='first-rebirth-v1')", "uid", (long)p.Entity.UID))
                                if (Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Recorded first-life recovery is missing.");
                        }
                    }
                    loaded.Remove(p); loaded.Add(p, state);
                } catch (Exception ex) { Q.Fail(p, "first-life-load", ex); throw; }
            }
            var info = state.Info;
            return info == null ? null : new ReincarnateInfo { UID = info.UID, Level = info.Level, Experience = info.Experience,
                FirstRebirth = true, Recovered = info.Recovered, RebirthClass = info.RebirthClass };
        }
        internal static void Consume(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ReincarnateInfo info) {
            if (p.Entity.Reborn != 1 || !info.FirstRebirth || info.Recovered || info.Level != p.Entity.FirstRebornLevel || info.RebirthClass / 10 != p.Entity.Class / 10)
                throw new InvalidOperationException("First-life recovery is not pending.");
            if (Q.Execute(c, t, @"UPDATE revora_first_rebirths SET recovered_at=CURRENT_TIMESTAMP
WHERE uid=@uid AND old_class=@old AND old_level=@level AND old_experience=@exp AND new_class=@next AND recovered_at IS NULL",
                "uid", (long)p.Entity.UID, "old", (int)p.Entity.FirstRebornClass, "level", (int)info.Level,
                "exp", checked((long)info.Experience), "next", (int)info.RebirthClass) != 1)
                throw new InvalidOperationException("First-life recovery receipt changed.");
        }
        internal static void Committed(GameState p, ReincarnateInfo info) {
            Loaded state;
            if (!loaded.TryGetValue(p, out state) || !ReferenceEquals(state.Entity, p.Entity) || state.Uid != p.Entity.UID
                || state.Info == null || state.Info.Level != info.Level || state.Info.Experience != info.Experience || state.Info.RebirthClass != info.RebirthClass)
                throw new InvalidOperationException("Committed first-life recovery requires reload.");
            state.Info.Recovered = true;
        }
    }
}
