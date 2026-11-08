using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ArmorDyeShop
    {
        private sealed class Selection { internal ConquerItem Item; internal uint Uid, Type; internal byte Slot, Colour; internal Guid Visit; }
        private static readonly ConditionalWeakTable<GameState, Selection> selections = new ConditionalWeakTable<GameState, Selection>();
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); using (var t = c.BeginTransaction()) {
                    Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_dye_visits(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),meteor_uid bigint NOT NULL CHECK(meteor_uid>0),
stack_before integer NOT NULL CHECK(stack_before BETWEEN 1 AND 65535),bound boolean NOT NULL,
paid_at timestamptz NOT NULL DEFAULT now(),entered_at timestamptz,closed_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_dye_open_visit ON revora_dye_visits(uid) WHERE closed_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_armor_dyes(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL CHECK(item_uid>0),
item_type integer NOT NULL,slot integer NOT NULL CHECK(slot IN(1,3)),colour_before integer NOT NULL,
colour_after integer NOT NULL CHECK(colour_after BETWEEN 2 AND 9),visit uuid REFERENCES revora_dye_visits(id),
material_uid bigint,created_at timestamptz NOT NULL DEFAULT now(),CHECK(colour_before<>colour_after),
CHECK((visit IS NOT NULL AND material_uid IS NULL AND colour_after BETWEEN 3 AND 9)
 OR (visit IS NULL AND material_uid IS NOT NULL AND material_uid>0 AND slot=3 AND colour_after=2)));
CREATE OR REPLACE FUNCTION revora_dye_map_changed() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF NEW.mapid=1008 AND OLD.mapid IS DISTINCT FROM NEW.mapid THEN
  UPDATE revora_dye_visits SET entered_at=COALESCE(entered_at,now()) WHERE uid=NEW.uid AND closed_at IS NULL;
 ELSIF OLD.mapid=1008 AND NEW.mapid IS DISTINCT FROM OLD.mapid THEN
  UPDATE revora_dye_visits SET closed_at=now() WHERE uid=NEW.uid AND closed_at IS NULL;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_dye_map_changed') THEN
CREATE TRIGGER revora_dye_map_changed AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_dye_map_changed();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_dye_map_changed' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_dye_map_changed()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_dye_map_changed';
END IF; END $revora_trigger$;");
                    t.Commit();
                }
            }
        }
        private static bool Near(GameState p, uint npc) { return (npc == 10063 || npc == 10064) && Q.Near(p, npc, npc == 10063 ? (ushort)1002 : (ushort)1008); }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) {
                var map = q.ExecuteScalar();
                if (map == null || Convert.ToUInt32(map) != p.Entity.MapID) throw new InvalidOperationException("Dye visitor location changed.");
            }
        }
        private static Guid Visit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, bool entered)
        {
            using (var q = Q.Command(c, t, @"SELECT id FROM revora_dye_visits WHERE uid=@uid AND closed_at IS NULL
AND (NOT @entered OR entered_at IS NOT NULL) FOR UPDATE", "uid", (long)p.Entity.UID, "entered", entered)) {
                object id = q.ExecuteScalar(); return id == null ? Guid.Empty : (Guid)id;
            }
        }
        private static bool Usable(ConquerItem item)
        { return item != null && item.IsWorn && item.StackSize == 1 && item.Warehouse == 0 && !item.Suspicious && !item.InWardrobe
            && (item.Days == 0 || DateTime.Now < item.DayStamp.AddDays(item.Days)); }
        private static ConquerItem Material(GameState p, uint type)
        { return p.Inventory.Objects.Where(i => i.ID == type && i.Position == 0 && i.StackSize > 0 && i.Warehouse == 0
            && i.Lock == 0 && !i.Suspicious && !i.InWardrobe && (i.Days == 0 || DateTime.Now < i.DayStamp.AddDays(i.Days)))
            .OrderByDescending(i => i.Bound).ThenBy(i => i.UID).FirstOrDefault(); }
        private static void Debit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ConquerItem item)
        {
            string sql = item.StackSize == 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
            if (Q.Execute(c, t, sql + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                throw new InvalidOperationException("Dye material ownership changed.");
        }
        private static void ProjectDebit(GameState p, ConquerItem item)
        {
            if (item == null) return;
            if (item.StackSize == 1) Q.Install(p, new[] { item.UID }, new ConquerItem[0]);
            else { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); }
        }
        internal static bool Admit(GameState p)
        {
            if (!Near(p, 10063)) return false;
            lock (p.ItemSyncRoot) {
                Map shop; INpc tinter;
                if (!Near(p, 10063) || !Kernel.Maps.TryGetValue(1008, out shop) || shop.Floor == null
                    || !shop.Npcs.TryGetValue(10064, out tinter) || !shop.Floor[22, 26, MapObjectType.Player, null]) return false;
                ConquerItem paid = null;
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        Owner(c, t, p);
                        if (Visit(c, t, p, false) == Guid.Empty) {
                            paid = Material(p, 1088001); if (paid == null) return false;
                            Debit(c, t, p, paid);
                            if (Q.Execute(c, t, @"INSERT INTO revora_dye_visits(id,uid,meteor_uid,stack_before,bound)
VALUES(@id,@uid,@item,@stack,@bound)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                                "item", (long)paid.UID, "stack", (int)paid.StackSize, "bound", paid.Bound) != 1)
                                throw new InvalidOperationException("Dye admission was not recorded.");
                        }
                        t.Commit();
                    }
                }
                ProjectDebit(p, paid); selections.Remove(p);
                p.Entity.Teleport(1008, 22, 26);
                return p.Entity.MapID == 1008;
            }
        }
        internal static bool Select(GameState p, byte slot, bool black)
        {
            uint npc = black ? 10063u : 10064u;
            if (!Near(p, npc) || slot != 1 && slot != 3 || black && slot != 3) return false;
            lock (p.ItemSyncRoot) {
                selections.Remove(p); if (!Near(p, npc)) return false;
                var item = p.Equipment.TryGetItem(slot);
                if (!Usable(item) || item.Position != slot || (byte)ItemHandler.GetPositionFromID(item.ID) != slot) return false;
                Guid visit = Guid.Empty;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    Owner(c, t, p);
                    if (!black && (visit = Visit(c, t, p, true)) == Guid.Empty) return false;
                    t.Commit();
                }}
                selections.Add(p, new Selection { Item = item, Uid = item.UID, Type = item.ID, Slot = slot, Colour = (byte)item.Color, Visit = visit });
                return true;
            }
        }
        internal static bool Dye(GameState p, byte colour, bool black)
        {
            uint npc = black ? 10063u : 10064u;
            if (!Near(p, npc) || (black ? colour != 2 : colour < 3 || colour > 9)) return false;
            lock (p.ItemSyncRoot) {
                Selection selected;
                if (!Near(p, npc) || !selections.TryGetValue(p, out selected)) return false;
                selections.Remove(p);
                var item = p.Equipment.TryGetItem(selected.Slot);
                if (black != (selected.Visit == Guid.Empty) || !ReferenceEquals(item, selected.Item) || !Usable(item)
                    || item.UID != selected.Uid || item.ID != selected.Type || item.Position != selected.Slot || (byte)item.Color != selected.Colour) return false;
                if ((byte)item.Color == colour) return true;
                ConquerItem paid = black ? Material(p, 1088000) : null;
                if (black && paid == null) return false;
                bool achievement = false;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    Owner(c, t, p);
                    if (!black && Visit(c, t, p, true) != selected.Visit) return false;
                    if (paid != null) Debit(c, t, p, paid);
                    if (Q.Execute(c, t, @"UPDATE items SET color=@colour WHERE uid=@item AND entityid=@uid AND id=@type
AND position=@slot AND warehouse=0 AND stacksize=1 AND bound=@bound AND COALESCE(color,0)=@before
AND locked=@locked AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "colour", (int)colour, "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID,
                        "slot", (int)selected.Slot, "bound", item.Bound ? 1 : 0, "before", (int)selected.Colour, "locked", (int)item.Lock,
                        "days", item.Days.ToString(CultureInfo.InvariantCulture), "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                        throw new InvalidOperationException("Dye equipment preimage changed.");
                    Guid receipt = Guid.NewGuid();
                    if (Q.Execute(c, t, @"INSERT INTO revora_armor_dyes(id,uid,item_uid,item_type,slot,colour_before,colour_after,visit,material_uid)
VALUES(@id,@uid,@item,@type,@slot,@before,@after,NULLIF(@visit,'00000000-0000-0000-0000-000000000000'::uuid),NULLIF(@material,0))",
                        "id", receipt, "uid", (long)p.Entity.UID, "item", (long)item.UID, "type", (int)item.ID,
                        "slot", (int)selected.Slot, "before", (int)selected.Colour, "after", (int)colour,
                        "visit", selected.Visit, "material", paid == null ? 0L : (long)paid.UID) != 1)
                        throw new InvalidOperationException("Armor dye was not recorded.");
                    if (selected.Slot == 3) {
                        achievement = ProgressionEvidence.Complete(c, t, p.Entity.UID, 10115, "armor-dye-v1", receipt.ToString("N"), 1);
                        using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10115)", "uid", (long)p.Entity.UID))
                            if (!Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Armor achievement was not recorded.");
                    }
                    t.Commit();
                }}
                ProjectDebit(p, paid); item.Color = (Enums.Color)colour; item.Mode = Enums.ItemMode.Update; item.Send(p);
                p.Equipment.UpdateEntityPacket(); if (achievement) Q.Refresh(p);
                return true;
            }
        }
        internal static void Dialog(GameState p, byte option)
        {
            if (!Near(p, p.ActiveNpc)) return;
            try {
                bool black = p.ActiveNpc == 10063;
                var d = new Npcs(p);
                if (black) {
                    if (option == 0) { d.Text("One Meteor opens my dye shop. Try as many colors as you like during your visit."); d.Option("Enter for one Meteor.", 1); d.Option("Dye my armor black.", 3); }
                    else if (option == 1) { if (Admit(p)) return; d.Text("Bring a Meteor and I will show you inside."); }
                    else if (option == 3) { if (!Select(p, 3, true)) d.Text("Put on the armor you want dyed."); else { d.Text("I can dye this armor black for one DragonBall."); d.Option("Dye it black.", 2); } }
                    else if (option == 2) d.Text(Dye(p, 2, true) ? "There. Wear it well." : "Keep your armor on and bring one DragonBall.");
                    else return;
                } else {
                    if (option == 0) { d.Text("Which piece shall I dye? Color changes are free during this visit."); d.Option("My helmet.", 1); d.Option("My armor.", 3); }
                    else if (option == 1 || option == 3) {
                        if (!Select(p, option, false)) d.Text("Wear your equipment and enter through Shopboy first.");
                        else { d.Text("Choose a color."); string[] names = { "Orange", "Light blue", "Red", "Blue", "Yellow", "Purple", "White" }; for (int i = 0; i < names.Length; i++) d.Option(names[i], (byte)(13 + i)); }
                    } else if (option >= 13 && option <= 19) { d.Text(Dye(p, (byte)(option - 10), false) ? "A fresh color. Try another if you wish." : "Choose the piece you are wearing again."); d.Option("Try another color.", 0); }
                    else return;
                }
                d.Option("Farewell.", 255); d.Avatar(7); d.Send();
            } catch (Exception ex) { Q.Fail(p, "armor-dye", ex); }
        }
    }
}
