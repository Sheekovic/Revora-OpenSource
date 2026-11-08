using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    // The durable preimage is separate from unflushed owner-session gains.
    internal static class SilverBalance
    {
        private sealed class Image { internal Entity Entity; internal long? Saved; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static void RequireOwner(GameState p)
        {
            if (p == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Silver requires a healthy owner lock.");
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
                    long? money = reader.ReadNullableInt64Strict("money");
                    if (money < 0) throw new OverflowException("Negative durable silver.");
                    images.Add(p, new Image { Entity = p.Entity, Saved = money });
                }
                catch (Exception ex) { QuestInventory.Fail(p, "silver-load", ex); throw; }
            }
        }
        internal static void PrepareSave(GameState p, MySqlCommand command)
        {
            RequireOwner(p); var image = State(p);
            if (image != null) { command.AndInt64Parameter("money", image.Saved); return; }
            // Bootstrap only an unchanged purse. Never infer a preimage from a
            // pending cache value or overwrite an offline credit before login.
            using (var q = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
            using (var r = new MySqlReader(q))
            {
                if (!r.Read()) throw new InvalidOperationException("Silver owner missing.");
                long? saved = r.ReadNullableInt64Strict("money");
                if (saved.GetValueOrDefault() != checked((long)p.Entity.Money))
                    throw new InvalidOperationException("Silver was not loaded.");
                command.AndInt64Parameter("money", saved);
            }
        }
        internal static void Committed(GameState p, ulong money)
        {
            RequireOwner(p);
            long saved = checked((long)money);
            Forget(p); images.Add(p, new Image { Entity = p.Entity, Saved = saved });
        }
        internal static void AssertSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            RequireOwner(p);
            if (t == null) throw new InvalidOperationException("Silver requires a transaction.");
            var image = State(p);
            using (var q = QuestInventory.Command(c, t, "SELECT money FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            {
                object value = q.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("Silver owner missing.");
                long? saved = value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
                if (saved < 0 || (image != null ? saved != image.Saved : saved.GetValueOrDefault() != checked((long)p.Entity.Money)))
                    throw new InvalidOperationException("Durable silver changed; reload required.");
            }
        }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ulong next)
        {
            AssertSaved(c, t, p);
            if (QuestInventory.Execute(c, t, "UPDATE entities SET money=@next WHERE uid=@uid", "next", checked((long)next), "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("Silver update was not saved.");
        }
        internal static void ApplyCommitted(GameState p, ulong money)
        {
            Committed(p, money); p.Entity.Money = money;
        }
        internal static void Save(GameState p)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                if (p.Entity.PersistenceRequiresReload) return;
                try
                {
                    ulong next = p.Entity.Money;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction()) { Persist(c, t, p, next); t.Commit(); }
                    }
                    Committed(p, next);
                }
                catch (Exception ex) { QuestInventory.Fail(p, "silver-save", ex); throw; }
            }
        }
        internal static void Credit(uint uid, uint amount)
        {
            GameState p;
            if (Kernel.GamePool.TryGetValue(uid, out p))
            {
                lock (p.ItemSyncRoot ?? p)
                {
                    GameState current;
                    if (p.Entity.FullyLoaded && Managed(p) && Kernel.GamePool.TryGetValue(uid, out current) && ReferenceEquals(p, current))
                    {
                        try
                        {
                            RequireOwner(p);
                            ulong next = checked(p.Entity.Money + amount);
                            using (var c = DataHolder.MySqlConnection)
                            {
                                c.Open(); using (var t = c.BeginTransaction()) { Persist(c, t, p, next); t.Commit(); }
                            }
                            ApplyCommitted(p, next); return;
                        }
                        catch (Exception ex) { QuestInventory.Fail(p, "silver-credit", ex); throw; }
                    }
                }
            }
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                if (QuestInventory.Execute(c, null, @"UPDATE entities SET money=COALESCE(money,0)+@amount
WHERE uid=@uid AND COALESCE(money,0)>=0 AND COALESCE(money,0)<=@maximum-@amount",
                    "uid", (long)uid, "amount", (long)amount, "maximum", long.MaxValue) != 1)
                    throw new InvalidOperationException("Offline silver credit could not be saved.");
            }
        }
    }
}
