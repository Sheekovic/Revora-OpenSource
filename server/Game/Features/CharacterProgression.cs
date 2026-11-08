using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    // The last durable progression image is distinct from pending in-memory gains.
    // A consumable reward can commit against it without overwriting another writer.
    internal static class CharacterProgression
    {
        internal static readonly string[] Columns = { "level", "experience", "strength", "agility", "vitality", "spirit", "atributes",
            "class", "reborn", "firstrebornclass", "secondrebornclass", "firstrebornlevel", "secondrebornlevel", "revora_attribute_bonus" };
        internal sealed class Profile
        {
            internal readonly long[] Values;
            internal readonly bool[] Nulls;
            internal Profile(params long[] values)
            {
                if (values == null || (values.Length != Columns.Length && values.Length != Columns.Length - 1)) throw new ArgumentException("Progression field count.");
                Values = new long[Columns.Length]; Array.Copy(values, Values, values.Length);
                Nulls = new bool[Columns.Length];
                for (int i = 0; i < Values.Length; i++) Validate(i, Values[i]);
            }
            internal Profile(long?[] values) : this(values.Select(value => value.GetValueOrDefault()).ToArray())
            { for (int i = 0; i < values.Length; i++) Nulls[i] = !values[i].HasValue; }
        }
        private sealed class LoadedState { internal Entity Entity; internal Profile Saved; internal bool Applying; }
        private static readonly ConditionalWeakTable<GameState, LoadedState> loaded = new ConditionalWeakTable<GameState, LoadedState>();
        private static void Validate(int index, long value)
        {
            long maximum = index == 13 ? 900 : index == 1 ? long.MaxValue : index >= 2 && index <= 6 ? ushort.MaxValue : byte.MaxValue;
            if (value < 0 || value > maximum) throw new OverflowException("Progression field exceeds storage range.");
        }
        private static void RequireOwner(GameState p)
        {
            if (p == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p)) throw new InvalidOperationException("Progression requires its owner lock.");
        }
        internal static Profile Capture(GameState p)
        {
            RequireOwner(p);
            var e = p.Entity;
            return new Profile(e.Level, checked((long)e.Experience), e.Strength, e.Agility, e.Vitality, e.Spirit, e.Atributes,
                e.Class, e.Reborn, e.FirstRebornClass, e.SecondRebornClass, e.FirstRebornLevel, e.SecondRebornLevel,
                State(p) == null ? 0 : State(p).Saved.Values[13]);
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) { c.Open(); QuestInventory.Execute(c, null,
                "DO $revora_column$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='revora_attribute_bonus' AND NOT attisdropped) THEN ALTER TABLE entities ADD COLUMN revora_attribute_bonus integer NOT NULL DEFAULT 0 CHECK(revora_attribute_bonus BETWEEN 0 AND 900); END IF; END $revora_column$"); }
        }
        // Rebirth planners calculate a fresh ordinary budget. Add owned bonuses once.
        internal static Profile PreserveEarnedAttributes(Profile planned, Profile before)
        {
            if (planned == null || before == null || planned.Values[13] != 0)
                throw new InvalidOperationException("Rebirth bonus must be applied exactly once.");
            var values = (long[])planned.Values.Clone();
            values[6] = checked((ushort)(values[6] + before.Values[13]));
            values[13] = before.Values[13]; return new Profile(values);
        }
        // Automatic first-life distributions can move earned points to the free
        // pool, but must preserve the budget. No bonus is inferred from old stats.
        internal static void AssignBaseStats(long[] values, DataHolder.BaseStats next, byte oldClass, byte oldLevel)
        {
            if (values[13] != 0)
            {
                var old = DataHolder.StatsFor(oldClass, oldLevel);
                long free = values.Skip(2).Take(5).Sum() - old.Strength - old.Agility - old.Vitality - old.Spirit;
                if (free < values[13] || free > ushort.MaxValue)
                    throw new InvalidOperationException("Earned attribute budget requires recovery.");
                values[6] = free;
            }
            values[2] = next.Strength; values[3] = next.Agility;
            values[4] = next.Vitality; values[5] = next.Spirit;
        }
        internal static void Forget(GameState p) { loaded.Remove(p); }
        internal static void Loaded(GameState p, MySqlReader reader)
        {
            Forget(p);
            try
            {
                var values = new long?[Columns.Length];
                for (int i = 0; i < values.Length; i++) values[i] = reader.ReadNullableInt64Strict(Columns[i]);
                Remember(p, new Profile(values));
            }
            catch (Exception ex) { QuestInventory.Fail(p, "progression-load", ex); throw; }
        }
        internal static void Remember(GameState p, Profile profile)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                loaded.Remove(p); loaded.Add(p, new LoadedState { Entity = p.Entity, Saved = profile });
            }
        }
        private static LoadedState State(GameState p)
        {
            LoadedState state;
            return loaded.TryGetValue(p, out state) && ReferenceEquals(state.Entity, p.Entity) ? state : null;
        }
        internal static bool Managed(GameState p) { return p != null && State(p) != null; }
        internal static bool IsColumn(string column)
        { return Array.Exists(Columns, name => string.Equals(name, column, StringComparison.OrdinalIgnoreCase)); }
        internal static Profile PrepareSave(GameState p, MySqlCommand command)
        {
            RequireOwner(p); var state = State(p); if (state == null) return null;
            if (p.Entity.PersistenceRequiresReload) throw new InvalidOperationException("Progression requires reload.");
            for (int i = 0; i < Columns.Length; i++) command.AndInt64Parameter(Columns[i], state.Saved.Nulls[i] ? (long?)null : state.Saved.Values[i]);
            return Capture(p);
        }
        internal static void Committed(GameState p, Profile profile)
        {
            RequireOwner(p); var state = State(p);
            if (state == null || profile == null) throw new InvalidOperationException("Progression was not loaded.");
            state.Saved = new Profile(profile.Values);
        }
        private static string Predicate { get { return string.Join(" AND ", Columns.Select((name, i) => name + " IS NOT DISTINCT FROM (CASE WHEN @null" + i + " THEN NULL::bigint ELSE @old" + i + " END)")); } }
        private static object[] Parameters(GameState p, Profile saved, Profile next)
        {
            var values = new object[2 + Columns.Length * 6]; values[0] = "uid"; values[1] = (long)p.Entity.UID;
            for (int i = 0; i < Columns.Length; i++)
            {
                values[2 + i * 6] = "old" + i; values[3 + i * 6] = saved.Values[i];
                values[4 + i * 6] = "next" + i; values[5 + i * 6] = next.Values[i];
                values[6 + i * 6] = "null" + i; values[7 + i * 6] = saved.Nulls[i];
            }
            return values;
        }
        internal static void Persist(NpgsqlConnection connection, NpgsqlTransaction transaction, GameState p, Profile next)
        {
            RequireOwner(p); var state = State(p);
            if (transaction == null || state == null || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Progression transaction is not ready.");
            next = new Profile(next.Values);
            string assignments = string.Join(",", Columns.Select((name, i) => name + "=@next" + i));
            int changed = QuestInventory.Execute(connection, transaction,
                "UPDATE entities SET " + assignments + " WHERE uid=@uid AND " + Predicate, Parameters(p, state.Saved, next));
            if (changed != 1) throw new InvalidOperationException("Saved progression changed.");
        }
        // Call only after the transaction commits, without releasing the owner lock.
        internal static void ApplyCommitted(GameState p, Profile profile)
        {
            RequireOwner(p); var state = State(p);
            if (state == null || p.Entity.PersistenceRequiresReload) throw new InvalidOperationException("Progression requires reload.");
            var values = profile.Values;
            state.Saved = new Profile(values); state.Applying = true;
            try
            {
                var e = p.Entity;
                if (e.Class != values[7]) e.Class = (byte)values[7];
                if (e.Reborn != values[8]) e.Reborn = (byte)values[8];
                if (e.FirstRebornClass != values[9]) e.FirstRebornClass = (byte)values[9];
                if (e.SecondRebornClass != values[10]) e.SecondRebornClass = (byte)values[10];
                e.FirstRebornLevel = (byte)values[11]; e.SecondRebornLevel = (byte)values[12];
                e.Strength = (ushort)values[2]; e.Agility = (ushort)values[3];
                e.Vitality = (ushort)values[4]; e.Spirit = (ushort)values[5]; e.Atributes = (ushort)values[6];
                if (e.Level != values[0]) e.Level = (byte)values[0];
                e.Experience = (ulong)values[1];
            }
            catch (Exception ex) { QuestInventory.Fail(p, "progression-projection", ex); throw; }
            finally { state.Applying = false; }
        }
        // Preserve legacy immediate byte-field writes while tracking their actual
        // durable result. Other progression fields are saved as part of SaveEntity.
        internal static bool TrySet(GameState p, string column, long value)
        {
            int index = Array.FindIndex(Columns, name => string.Equals(name, column, StringComparison.OrdinalIgnoreCase));
            if (p == null || index < 0) return false;
            if (p.Entity.PersistenceRequiresReload) throw new InvalidOperationException("Progression requires reload.");
            if (!Managed(p)) return false;
            lock (p.ItemSyncRoot ?? p)
            {
                if (p.Entity.PersistenceRequiresReload) throw new InvalidOperationException("Progression requires reload.");
                try
                {
                    Validate(index, value); var state = State(p);
                    if (state.Applying)
                    {
                        if (state.Saved.Values[index] != value) throw new InvalidOperationException("Committed progression projection differs.");
                        return true;
                    }
                    var changed = (long[])state.Saved.Values.Clone(); changed[index] = value;
                    var next = new Profile(changed);
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction()) { Persist(c, t, p, next); t.Commit(); }
                    }
                    Committed(p, next); return true;
                }
                catch (Exception ex) { QuestInventory.Fail(p, "progression-write", ex); throw; }
            }
        }
    }
}
