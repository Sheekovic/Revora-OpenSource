using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    internal static class TimeDropBalance
    {
        private sealed class Image { internal Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static void RequireOwner(GameState p)
        {
            if (p == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("TimeDrop requires a healthy owner lock.");
        }
        private static Image State(GameState p)
        {
            Image image;
            return images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity) ? image : null;
        }
        internal static bool Managed(GameState p) { return p != null && State(p) != null; }
        internal static void Forget(GameState p) { images.Remove(p); }
        internal static void Loaded(GameState p, MySqlReader reader)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                Forget(p);
                try
                {
                    long? seconds = reader.ReadNullableInt64Strict("timedropslayer");
                    if (seconds < 0 || seconds > ushort.MaxValue) throw new OverflowException("Invalid durable TimeDrop.");
                    images.Add(p, new Image { Entity = p.Entity, Saved = seconds });
                }
                catch (Exception ex) { QuestInventory.Fail(p, "timedrop-load", ex); throw; }
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command)
        {
            RequireOwner(p); var image = State(p);
            if (image != null) { command.AndInt64Parameter("timedropslayer", image.Saved); return; }
            using (var q = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
            using (var r = new MySqlReader(q))
            {
                if (!r.Read()) throw new InvalidOperationException("TimeDrop owner missing.");
                long? saved = r.ReadNullableInt64Strict("timedropslayer");
                if (saved.GetValueOrDefault() != checked((long)p.Entity.TimeDropSlayer))
                    throw new InvalidOperationException("TimeDrop was not loaded.");
                command.AndInt64Parameter("timedropslayer", saved);
            }
        }
        internal static void Committed(GameState p, ushort seconds)
        {
            RequireOwner(p);
            long saved = checked((long)seconds);
            Forget(p); images.Add(p, new Image { Entity = p.Entity, Saved = saved });
        }
        internal static void AssertSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            RequireOwner(p);
            if (t == null) throw new InvalidOperationException("TimeDrop requires a transaction.");
            var image = State(p);
            using (var q = QuestInventory.Command(c, t, "SELECT timedropslayer FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            {
                object value = q.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("TimeDrop owner missing.");
                long? saved = value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
                if (saved < 0 || saved > ushort.MaxValue || (image != null ? saved != image.Saved : saved.GetValueOrDefault() != checked((long)p.Entity.TimeDropSlayer)))
                    throw new InvalidOperationException("Durable TimeDrop changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ushort next)
        {
            AssertSaved(c, t, p);
            if (QuestInventory.Execute(c, t, "UPDATE entities SET timedropslayer=@next WHERE uid=@uid", "next", checked((long)next), "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("TimeDrop update was not saved.");
        }
        internal static void ApplyCommitted(GameState p, ushort seconds)
        {
            Committed(p, seconds); p.Entity.InstallTimeDropSlayer(seconds);
        }
        internal static void Set(GameState p, ushort next)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                if (!QuestInventory.Player(p, false)) throw new InvalidOperationException("TimeDrop grant requires the current character session.");
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction()) { Persist(c, t, p, next); t.Commit(); }
                    }
                    ApplyCommitted(p, next);
                }
                catch (Exception ex) { QuestInventory.Fail(p, "timedrop-save", ex); throw; }
            }
        }
    }
}
