using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    // The durable preimage is separate from unflushed owner-session gains.
    internal static class BlessingBalance
    {
        private sealed class Image { internal Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static void RequireOwner(GameState p)
        {
            if (p == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Blessing requires a healthy owner lock.");
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
                    long? seconds = reader.ReadNullableInt64Strict("heavenblessingtime");
                    if (seconds < 0 || seconds > uint.MaxValue) throw new OverflowException("Invalid durable blessing.");
                    images.Add(p, new Image { Entity = p.Entity, Saved = seconds });
                }
                catch (Exception ex) { QuestInventory.Fail(p, "blessing-load", ex); throw; }
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command)
        {
            RequireOwner(p); var image = State(p);
            if (image != null) { command.AndInt64Parameter("heavenblessingtime", image.Saved); return; }
            // Bootstrap only unchanged time. Never infer a saved preimage from
            // pending countdown ticks or overwrite a newer durable grant.
            using (var q = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
            using (var r = new MySqlReader(q))
            {
                if (!r.Read()) throw new InvalidOperationException("Blessing owner missing.");
                long? saved = r.ReadNullableInt64Strict("heavenblessingtime");
                if (saved.GetValueOrDefault() != checked((long)p.Entity.HeavenBlessing))
                    throw new InvalidOperationException("Blessing was not loaded.");
                command.AndInt64Parameter("heavenblessingtime", saved);
            }
        }
        internal static void Committed(GameState p, uint seconds)
        {
            RequireOwner(p);
            long saved = checked((long)seconds);
            Forget(p); images.Add(p, new Image { Entity = p.Entity, Saved = saved });
        }
        internal static void AssertSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            RequireOwner(p);
            if (t == null) throw new InvalidOperationException("Blessing requires a transaction.");
            var image = State(p);
            using (var q = QuestInventory.Command(c, t, "SELECT heavenblessingtime FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            {
                object value = q.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("Blessing owner missing.");
                long? saved = value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
                if (saved < 0 || saved > uint.MaxValue || (image != null ? saved != image.Saved : saved.GetValueOrDefault() != checked((long)p.Entity.HeavenBlessing)))
                    throw new InvalidOperationException("Durable blessing changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint next)
        {
            AssertSaved(c, t, p);
            if (QuestInventory.Execute(c, t, "UPDATE entities SET heavenblessingtime=@next WHERE uid=@uid", "next", checked((long)next), "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("Blessing update was not saved.");
        }
        internal static void ApplyCommitted(GameState p, uint seconds)
        {
            Committed(p, seconds); p.Entity.InstallHeavenBlessing(seconds);
        }
        internal static void Grant(GameState p, uint seconds)
        {
            lock (p.ItemSyncRoot ?? p)
            lock (MentorExperience.Sync)
            {
                if (!QuestInventory.Player(p, false)) throw new InvalidOperationException("Blessing grant requires the current character session.");
                try
                {
                    uint next = checked(p.Entity.HeavenBlessing + seconds);
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            // Generic quest/event grants do not earn mentor contribution.
                            AssertSaved(c, t, p);
                            Persist(c, t, p, next); t.Commit();
                        }
                    }
                    ApplyCommitted(p, next);
                }
                catch (Exception ex) { QuestInventory.Fail(p, "blessing-grant", ex); throw; }
            }
        }
        internal static void Set(GameState p, uint next)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                if (!QuestInventory.Player(p, false)) throw new InvalidOperationException("Blessing grant requires the current character session.");
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction()) { Persist(c, t, p, next); t.Commit(); }
                    }
                    ApplyCommitted(p, next);
                }
                catch (Exception ex) { QuestInventory.Fail(p, "blessing-save", ex); throw; }
            }
        }
    }
}
