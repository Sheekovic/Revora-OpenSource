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
    internal static class SecondRebirthRecovery
    {
        private sealed class Loaded { internal Entity Entity; internal uint Uid; internal ReincarnateInfo Info; }
        private static readonly ConditionalWeakTable<GameState, Loaded> loaded = new ConditionalWeakTable<GameState, Loaded>();
        internal static ReincarnateInfo Snapshot(GameState p) {
            if (!Monitor.IsEntered(p.ItemSyncRoot ?? p)) throw new InvalidOperationException("Second-life recovery requires its owner lock.");
            if (p.Fake || p.Entity.Reborn != 2 || p.Entity.SecondRebornLevel < 130) return null;
            Loaded state;
            if (!loaded.TryGetValue(p, out state) || !ReferenceEquals(state.Entity, p.Entity) || state.Uid != p.Entity.UID) {
                state = new Loaded { Entity = p.Entity, Uid = p.Entity.UID };
                try {
                    using (var c = DataHolder.MySqlConnection) { c.Open();
                        using (var q = Q.Command(c, null, "SELECT old_class,old_level,old_experience,new_class,recovered_at,first_class,first_level FROM revora_second_rebirths WHERE uid=@uid", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) if (r.Read()) {
                            byte level = checked((byte)Convert.ToInt32(r[1]));
                            byte oldClass = checked((byte)Convert.ToInt32(r[0])), nextClass = checked((byte)Convert.ToInt32(r[3]));
                            if (!SecondRebirthRules.Eligible(oldClass, level, 1) || Array.IndexOf(RebirthRules.Classes, nextClass) < 0
                                || r.IsDBNull(4) && (Convert.ToInt32(r[5]) != p.Entity.FirstRebornClass || Convert.ToInt32(r[6]) != p.Entity.FirstRebornLevel
                                    || oldClass != p.Entity.SecondRebornClass || level != p.Entity.SecondRebornLevel || nextClass / 10 != p.Entity.Class / 10))
                                throw new InvalidOperationException("Second-life receipt differs from its character.");
                            state.Info = new ReincarnateInfo { UID = p.Entity.UID, Level = level, Experience = checked((ulong)Convert.ToInt64(r[2])),
                                SecondRebirth = true, Recovered = !r.IsDBNull(4), RebirthClass = nextClass };
                            ReincarnationRecovery.Validate(state.Info);
                        }
                        if (state.Info == null) {
                            using (var q = Q.Command(c, null, "SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND source='second-rebirth-v1')", "uid", (long)p.Entity.UID))
                                if (Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Recorded second-life recovery is missing.");
                        }
                    }
                    loaded.Remove(p); loaded.Add(p, state);
                } catch (Exception ex) { Q.Fail(p, "second-life-load", ex); throw; }
            }
            var info = state.Info;
            return info == null ? null : new ReincarnateInfo { UID = info.UID, Level = info.Level, Experience = info.Experience,
                SecondRebirth = true, Recovered = info.Recovered, RebirthClass = info.RebirthClass };
        }
        internal static void Consume(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ReincarnateInfo info) {
            if (p.Entity.Reborn != 2 || !info.SecondRebirth || info.Recovered || info.Level != p.Entity.SecondRebornLevel || info.RebirthClass / 10 != p.Entity.Class / 10)
                throw new InvalidOperationException("Second-life recovery is not pending.");
            if (Q.Execute(c, t, @"UPDATE revora_second_rebirths SET recovered_at=CURRENT_TIMESTAMP
WHERE uid=@uid AND old_class=@old AND old_level=@level AND old_experience=@exp AND new_class=@next AND first_class=@first AND first_level=@firstlevel AND recovered_at IS NULL",
                "first", (int)p.Entity.FirstRebornClass, "firstlevel", (int)p.Entity.FirstRebornLevel, "uid", (long)p.Entity.UID, "old", (int)p.Entity.SecondRebornClass, "level", (int)info.Level,
                "exp", checked((long)info.Experience), "next", (int)info.RebirthClass) != 1)
                throw new InvalidOperationException("Second-life recovery receipt changed.");
        }
        internal static void Committed(GameState p, ReincarnateInfo info) {
            Loaded state;
            if (!loaded.TryGetValue(p, out state) || !ReferenceEquals(state.Entity, p.Entity) || state.Uid != p.Entity.UID
                || state.Info == null || state.Info.Level != info.Level || state.Info.Experience != info.Experience || state.Info.RebirthClass != info.RebirthClass)
                throw new InvalidOperationException("Committed second-life recovery requires reload.");
            state.Info.Recovered = true;
        }
    }
}
