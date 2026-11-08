using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // The saved image must not be inferred from unflushed kill/decay changes.
    internal static class PkBalance
    {
        private sealed class Image { internal Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static Image State(GameState p) {
            Image image;
            return images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity) ? image : null;
        }
        private static void Require(GameState p) {
            if (p == null || p.Entity == null || p.Entity.PersistenceRequiresReload
                || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || !Monitor.IsEntered(p.PkSyncRoot))
                throw new InvalidOperationException("PK persistence requires both healthy owner locks.");
        }
        private static long? Valid(long? points) {
            if (points < 0 || points > ushort.MaxValue) throw new OverflowException("Stored PK points are outside their range.");
            return points;
        }
        internal static void Forget(GameState p) { images.Remove(p); }
        internal static void Loaded(GameState p, MySqlReader reader) {
            lock (p.ItemSyncRoot ?? p) lock (p.PkSyncRoot) {
                Forget(p);
                try { images.Add(p, new Image { Entity = p.Entity, Saved = Valid(reader.ReadNullableInt64Strict("pkpoints")) }); }
                catch (Exception ex) { Q.Fail(p, "pk-load", ex); throw; }
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command) {
            Require(p); var image = State(p);
            if (image != null) { command.AndInt64Parameter("pkpoints", image.Saved); return; }
            using (var query = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
            using (var reader = new MySqlReader(query)) {
                if (!reader.Read()) throw new InvalidOperationException("PK owner missing.");
                long? saved = Valid(reader.ReadNullableInt64Strict("pkpoints"));
                if (saved.GetValueOrDefault() != p.Entity.PKPoints) throw new InvalidOperationException("PK points were not loaded.");
                command.AndInt64Parameter("pkpoints", saved);
            }
        }
        internal static void Committed(GameState p) {
            Require(p); Forget(p); images.Add(p, new Image { Entity = p.Entity, Saved = p.Entity.PKPoints });
        }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ushort next) {
            Require(p);
            if (t == null) throw new InvalidOperationException("PK persistence requires a transaction.");
            var image = State(p);
            using (var query = Q.Command(c, t, "SELECT pkpoints FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) {
                object value = query.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("PK owner missing.");
                long? saved = Valid(value == DBNull.Value ? (long?)null : Convert.ToInt64(value));
                if (image != null ? saved != image.Saved : saved.GetValueOrDefault() != p.Entity.PKPoints)
                    throw new InvalidOperationException("Durable PK points changed; reload required.");
            }
            if (Q.Execute(c, t, "UPDATE entities SET pkpoints=@next WHERE uid=@uid", "next", (int)next, "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("PK balance was not saved.");
        }
        internal static void ApplyCommitted(GameState p, ushort next) {
            Require(p); p.Entity.PKPoints = next; Committed(p);
        }
    }
}
