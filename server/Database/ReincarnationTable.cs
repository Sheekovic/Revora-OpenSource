// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using Revora.Game.Features.Reincarnation;
using Revora.Game.Features;
using System;

namespace Revora.Database
{
    public unsafe class ReincarnationTable
    {
        public static void Load()
        {
            var seen = new System.Collections.Generic.HashSet<uint>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("reincarnation"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    ReincarnateInfo info = new ReincarnateInfo();
                    info.UID = checked((uint)(reader.ReadNullableInt64Strict("uid") ?? -1));
                    info.Level = checked((byte)(reader.ReadNullableInt64Strict("level") ?? -1));
                    info.Experience = checked((ulong)(reader.ReadNullableInt64Strict("experience") ?? -1));
                    ReincarnationRecovery.Validate(info);
                    if (!seen.Add(info.UID)) throw new InvalidOperationException("Duplicate reincarnation owner.");
                    ReincarnateInfo previous;
                    if (!Kernel.ReincarnatedCharacters.TryAdd(info.UID, info)
                        && Kernel.ReincarnatedCharacters.TryGetValue(info.UID, out previous)
                        && (previous.Level != info.Level || previous.Experience != info.Experience))
                        throw new InvalidOperationException("Conflicting reincarnation snapshots.");
                }
            }
        }
        public static void NewReincarnated(Game.Entity entity)
        {
            lock (entity.Owner.ItemSyncRoot ?? entity.Owner)
            {
                var info = new ReincarnateInfo { UID = entity.UID, Level = entity.Level, Experience = entity.Experience };
                ReincarnationRecovery.Validate(info);
                if (entity.PersistenceRequiresReload) throw new InvalidOperationException("Character requires reload.");
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        using (var q = QuestInventory.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)entity.UID))
                            if (q.ExecuteScalar() == null) throw new InvalidOperationException("Reincarnation owner missing.");
                        using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM reincarnation WHERE uid=@uid", "uid", (long)entity.UID))
                            if (Convert.ToInt64(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Reincarnation already pending.");
                        QuestInventory.Execute(c, t, "INSERT INTO reincarnation(uid,level,experience) VALUES(@uid,@level,@exp)",
                            "uid", (long)info.UID, "level", (long)info.Level, "exp", (long)info.Experience);
                        t.Commit();
                    }
                }
            }
        }
    }
}
