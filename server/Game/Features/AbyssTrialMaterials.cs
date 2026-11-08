using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Transaction participants only. Callers commit before applying the returned
    // inventory change; a failed projection must fence the session for reload.
    internal static class AbyssTrialMaterials
    {
        internal sealed class Part {
            internal readonly ConquerItem Item;
            internal readonly uint Uid, Type;
            internal readonly ushort Stack, Count;
            internal readonly bool Bound;
            internal Part(ConquerItem item, int count) {
                Item = item; Uid = item.UID; Type = item.ID; Stack = item.StackSize;
                Count = checked((ushort)count); Bound = item.Bound;
            }
            internal int After { get { return Math.Max(1, (int)Stack) - Count; } }
        }
        internal sealed class Plan {
            internal readonly Part[] Parts;
            internal Plan(Part[] parts) { Parts = parts; }
            internal bool Bound { get { return Parts.Any(p => p.Bound); } }
            internal int Freed { get { return Parts.Count(p => p.After == 0); } }
        }
        internal static bool Usable(ConquerItem i) {
            return i != null && i.ID >= 722721 && i.ID <= 722731 && i.Position == 0 && i.Warehouse == 0
                && i.Lock == 0 && !i.Suspicious && !i.InWardrobe && i.Days == 0 && i.TimeLeftInMinutes == 0;
        }
        internal static Plan Select(GameState p, AbyssTrialRules.Material[] costs) {
            if (p == null || !Monitor.IsEntered(p.ItemSyncRoot) || costs == null || costs.Length == 0
                || costs.Any(c => c.Type < 722721 || c.Type > 722731 || c.Count == 0)
                || costs.Select(c => c.Type).Distinct().Count() != costs.Length) throw new ArgumentException("Invalid Abyss material selection.");
            var parts = new List<Part>();
            foreach (var cost in costs) {
                int need = cost.Count;
                foreach (var i in p.Inventory.Objects.Where(i => i.ID == cost.Type && Usable(i)).OrderByDescending(i => i.Bound).ThenBy(i => i.UID)) {
                    int take = Math.Min(need, Math.Max(1, (int)i.StackSize)); parts.Add(new Part(i, take)); need -= take;
                    if (need == 0) break;
                }
                if (need != 0) return null;
            }
            return new Plan(parts.ToArray());
        }
        internal static void Bag(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            SkillTable.LockOwner(c, t, p);
            var ids = new List<uint>();
            using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) while (r.Read()) {
                if (ids.Count >= 40) throw new InvalidOperationException("Abyss inventory exceeds its bound."); ids.Add(checked((uint)Convert.ToInt64(r[0])));
            }
            if (!ids.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i))) throw new InvalidOperationException("Abyss inventory requires reload.");
        }
        internal static void Debit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Guid operation, Plan plan, AbyssTrialRules.Material[] costs) {
            Bag(c, t, p);
            if (plan == null || plan.Parts.Length == 0 || plan.Parts.Select(x => x.Uid).Distinct().Count() != plan.Parts.Length
                || plan.Parts.Select(x => x.Type).Distinct().Count() != costs.Length
                || costs.Any(cost => plan.Parts.Where(x => x.Type == cost.Type).Sum(x => (int)x.Count) != cost.Count))
                throw new InvalidOperationException("Abyss offering does not match its recipe.");
            foreach (var part in plan.Parts) {
                ConquerItem item;
                if (part.Count == 0 || part.After < 0 || !p.Inventory.TryGetItem(part.Uid, out item) || !ReferenceEquals(item, part.Item)
                    || !Usable(item) || item.ID != part.Type || item.StackSize != part.Stack || item.Bound != part.Bound)
                    throw new InvalidOperationException("Abyss offering changed.");
                string sql = part.After == 0 ? "DELETE FROM items" : "UPDATE items SET stacksize=@after";
                if (Q.Execute(c, t, sql + @" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'", "after", part.After, "item", (long)part.Uid, "uid", (long)p.Entity.UID,
                    "type", (int)part.Type, "stack", (int)part.Stack, "bound", part.Bound ? 1 : 0) != 1)
                    throw new InvalidOperationException("Abyss material ownership changed.");
                if (Q.Execute(c, t, @"INSERT INTO revora_abyss_materials(operation,item_uid,item_type,before_stack,consumed,bound)
VALUES(@operation,@item,@type,@stack,@count,@bound)", "operation", operation, "item", (long)part.Uid, "type", (int)part.Type,
                    "stack", (int)part.Stack, "count", (int)part.Count, "bound", part.Bound) != 1)
                    throw new InvalidOperationException("Abyss offering receipt was not saved.");
            }
        }
        internal static ConquerItem Insert(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint type, bool bound) {
            if (type < 722721 || type > 722731) throw new ArgumentException("Unknown Abyss reward.");
            var item = Q.New(type, bound);
            if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)", "item", (long)item.UID, "type", (int)type, "uid", (long)p.Entity.UID,
                "bound", bound ? 1 : 0, "dur", (int)item.Durability, "max", (int)item.MaximDurability) != 1)
                throw new InvalidOperationException("Abyss reward was not saved.");
            return item;
        }
        internal static void Apply(GameState p, Plan plan, ConquerItem added) {
            if (!Monitor.IsEntered(p.ItemSyncRoot)) throw new InvalidOperationException("Abyss projection requires its owner lock.");
            if (plan != null) foreach (var part in plan.Parts) {
                if (part.After == 0) Q.Install(p, new[] { part.Uid }, new ConquerItem[0]);
                else { part.Item.StackSize = checked((ushort)part.After); part.Item.Mode = Enums.ItemMode.Update; part.Item.Send(p); }
            }
            if (added != null) Q.Install(p, new uint[0], new[] { added });
        }
    }
}
