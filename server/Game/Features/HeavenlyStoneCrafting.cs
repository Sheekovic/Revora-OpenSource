using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HeavenlyStoneCrafting
    {
        internal const uint Sereth = 59558, Stone = 721259;
        private static readonly uint[] recipe = { 700001, 700011, 700021, 700031, 700041, 700051, 700061, 721258 };
        private sealed class Take { internal ConquerItem Item; internal uint Uid, Type; internal ushort Before; internal bool Bound; }
        private sealed class Offer { internal Guid Id; internal DateTime Until; internal List<Take> Materials; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static void Initialize() {
            foreach (uint id in recipe.Concat(new[] { Stone }))
                if (!ConquerItemInformation.BaseInformations.ContainsKey(id)) throw new InvalidOperationException("Heavenly Stone recipe catalog is missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_heavenly_stone_crafts(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),
bound boolean NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_heavenly_stone_materials(
craft uuid NOT NULL REFERENCES revora_heavenly_stone_crafts(id),item_uid bigint NOT NULL CHECK(item_uid>0),
item_type integer NOT NULL CHECK(item_type IN(700001,700011,700021,700031,700041,700051,700061,721258)),
stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),bound boolean NOT NULL,
PRIMARY KEY(craft,item_uid),UNIQUE(craft,item_type));"); }
        }
        private static bool Near(GameState p) { return Q.Near(p, Sereth, 1002) && p.Map.ID == 1002 && p.Map.BaseID == 1002; }
        private static bool Usable(ConquerItem i) {
            return i != null && i.Position == 0 && i.Warehouse == 0 && i.Lock == 0 && !i.Suspicious && !i.InWardrobe
                && i.Days == 0 && i.TimeLeftInMinutes == 0;
        }
        private static List<Take> Select(GameState p) {
            var selected = new List<Take>();
            foreach (uint type in recipe) {
                var item = p.Inventory.Objects.Where(i => i.ID == type && Usable(i)).OrderByDescending(i => i.Bound).ThenBy(i => i.UID).FirstOrDefault();
                if (item == null) return null;
                selected.Add(new Take { Item = item, Uid = item.UID, Type = item.ID, Before = item.StackSize, Bound = item.Bound });
            }
            return selected;
        }
        private static bool Same(GameState p, Take take) {
            ConquerItem item; return p.Inventory.TryGetItem(take.Uid, out item) && ReferenceEquals(item, take.Item) && Usable(item)
                && item.ID == take.Type && item.StackSize == take.Before && item.Bound == take.Bound;
        }
        private static bool Room(GameState p, Offer offer) { return p.Inventory.Count - offer.Materials.Count(m => m.Before <= 1) < 40; }
        private static void Prepare(GameState p, Npcs dialog) {
            offers.Remove(p); var materials = Select(p);
            if (materials == null) { dialog.Text("Bring seven normal gems: Phoenix, Dragon, Fury, Rainbow, Kylin, Violet and Moon, plus Clean Water. Keep them unlocked in your bag."); return; }
            var offer = new Offer { Id = Guid.NewGuid(), Until = Clock().AddMinutes(2), Materials = materials };
            if (!Room(p, offer)) { dialog.Text("Make room in your bag for the Heavenly Stone."); return; }
            offers.Add(p, offer);
            dialog.Text("I will bind these seven gems and Clean Water into one Heavenly Stone" + (materials.Any(m => m.Bound) ? ", bound to you." : "."));
            dialog.Option("Forge the stone.", 39);
        }
        private static void Craft(GameState p, Npcs dialog) {
            Offer offer;
            if (!offers.TryGetValue(p, out offer)) { dialog.Text("Show me your materials first."); return; }
            offers.Remove(p);
            if (Clock() >= offer.Until || !offer.Materials.All(m => Same(p, m))) { dialog.Text("Show me your materials again."); return; }
            if (!Room(p, offer)) { dialog.Text("Make room in your bag for the Heavenly Stone."); return; }
            var result = Q.New(Stone, offer.Materials.Any(m => m.Bound));
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                    if (Convert.ToUInt32(q.ExecuteScalar()) != 1002) throw new InvalidOperationException("Heavenly Stone owner location changed.");
                using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Heavenly Stone inventory changed.");
                foreach (var take in offer.Materials.OrderBy(m => m.Uid)) {
                    string sql = take.Before <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, sql + @" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'", "item", (long)take.Uid, "type", (int)take.Type, "uid", (long)p.Entity.UID,
                        "before", (int)take.Before, "bound", take.Bound ? 1 : 0) != 1)
                        throw new InvalidOperationException("Heavenly Stone material ownership changed.");
                }
                if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,721259,@uid,0,0,1,@bound,@dur,@max)", "item", (long)result.UID, "uid", (long)p.Entity.UID,
                    "bound", result.Bound ? 1 : 0, "dur", (int)result.Durability, "max", (int)result.MaximDurability) != 1)
                    throw new InvalidOperationException("Heavenly Stone reward was not saved.");
                if (Q.Execute(c, t, "INSERT INTO revora_heavenly_stone_crafts(id,uid,item_uid,bound) VALUES(@id,@uid,@item,@bound)",
                    "id", offer.Id, "uid", (long)p.Entity.UID, "item", (long)result.UID, "bound", result.Bound) != 1)
                    throw new InvalidOperationException("Heavenly Stone craft was not recorded.");
                foreach (var take in offer.Materials)
                    if (Q.Execute(c, t, @"INSERT INTO revora_heavenly_stone_materials(craft,item_uid,item_type,stack_before,bound)
VALUES(@craft,@item,@type,@before,@bound)", "craft", offer.Id, "item", (long)take.Uid, "type", (int)take.Type,
                        "before", (int)take.Before, "bound", take.Bound) != 1)
                        throw new InvalidOperationException("Heavenly Stone material receipt was not saved.");
                if (!Near(p) || Clock() >= offer.Until || !offer.Materials.All(m => Same(p, m)))
                    throw new InvalidOperationException("Heavenly Stone session changed.");
                t.Commit();
            }}
            Q.Install(p, offer.Materials.Where(m => m.Before <= 1).Select(m => m.Uid), new[] { result });
            foreach (var take in offer.Materials.Where(m => m.Before > 1)) {
                take.Item.StackSize = (ushort)(take.Before - 1); take.Item.Mode = Enums.ItemMode.Update; take.Item.Send(p); take.Item.Mode = Enums.ItemMode.Default;
            }
            dialog.Text("Your Heavenly Stone is ready. Bring it back when you are ready for your first rebirth.");
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ItemSyncRoot == null) return false;
            bool handled = p.ActiveNpc == Sereth && (request.OptionID == 30 || request.OptionID == 31 || request.OptionID == 38 || request.OptionID == 39);
            lock (p.ItemSyncRoot) {
                if (!handled) { offers.Remove(p); return false; }
                if (request.NpcID != 0 && request.NpcID != Sereth || !Near(p)) { offers.Remove(p); return true; }
                try {
                    var dialog = new Npcs(p); dialog.Avatar(15);
                    if (request.OptionID == 30 || request.OptionID == 31) {
                        offers.Remove(p);
                        dialog.Text(request.OptionID == 30 ? "Seven normal gems and Clean Water make a Heavenly Stone. I can forge it for you."
                            : "Waterlord guards Clean Water beyond the Adventure Islands. The guild-map transporter can take you to Old Explorer.");
                        dialog.Option("Show my materials.", 38);
                        if (request.OptionID == 30) dialog.Option("Where is Clean Water?", 31);
                    } else if (request.OptionID == 38) Prepare(p, dialog); else Craft(p, dialog);
                    dialog.Option("Farewell.", 255); dialog.Send();
                } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "heavenly-stone-craft", ex); }
            }
            return true;
        }
    }
}
