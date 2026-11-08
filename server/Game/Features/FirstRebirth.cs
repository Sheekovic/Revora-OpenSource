using System;
using System.Collections.Generic;
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
    internal static class FirstRebirth
    {
        private sealed class Offer {
            internal byte Class, Stage; internal ushort Body; internal bool Bless;
            internal DateTime Until; internal string Cache, Items, Skills, Profs;
        }
        private sealed class Gear { internal ConquerItem Item; internal uint Type; internal byte Position, Bless; internal bool Bound; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Func<int, int> Roll = maximum => Kernel.Random.Next(maximum);
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_first_rebirths(
uid bigint PRIMARY KEY,old_class integer NOT NULL,old_level integer NOT NULL,old_experience bigint NOT NULL,
new_class integer NOT NULL,body integer NOT NULL,stone_uid bigint NOT NULL,bound boolean NOT NULL,
reward_item bigint NOT NULL,blessed_item bigint NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
ALTER TABLE revora_first_rebirths ADD COLUMN IF NOT EXISTS garment_uid bigint UNIQUE;
ALTER TABLE revora_first_rebirths ADD COLUMN IF NOT EXISTS recovered_at timestamptz;"); }
        }
        private static bool Near(GameState p) {
            return Q.Near(p, HeavenlyStoneCrafting.Sereth, 1002) && p.Map.ID == 1002 && p.Map.BaseID == 1002
                && p.Entity.PokerTable == 0 && CharacterProgression.Managed(p);
        }
        private static bool Stone(ConquerItem i) {
            return i.ID == HeavenlyStoneCrafting.Stone && i.Position == 0 && i.Warehouse == 0 && i.Lock == 0
                && !i.Suspicious && !i.InWardrobe && i.Days == 0 && i.TimeLeftInMinutes == 0;
        }
        internal static string Cache(GameState p) {
            return string.Join(",", CharacterProgression.Capture(p).Values) + ":" + p.Entity.Body + ":" + p.Entity.Face + ":" + p.Entity.HairStyle
                + "|" + string.Join(";", p.Inventory.Objects.Concat(p.Equipment.GetItems()).OrderBy(i => i.UID).Select(i =>
                    i.UID + ":" + Convert.ToBase64String(i.Buffer) + ":" + i.Warehouse + ":" + i.Days + ":" + i.TimeLeftInMinutes))
                + "|" + string.Join(";", p.Spells.Values.OrderBy(s => s.ID).Select(s => s.ID + ":" + s.Level + ":" + s.PreviousLevel + ":" + s.Experience + ":" + s.Souls + ":" + s.LevelHu2))
                + "|" + string.Join(";", p.Proficiencies.Values.OrderBy(s => s.ID).Select(s => s.ID + ":" + s.Level + ":" + s.PreviousLevel + ":" + s.Experience));
        }
        // Whole owned rows preserve fields this rite does not change. Names are an explicit constant allowlist.
        internal static string Rows(NpgsqlConnection c, NpgsqlTransaction t, GameState p, string table) {
            if (table != "items" && table != "skills" && table != "profs") throw new ArgumentException("Rebirth snapshot table.");
            string filter = table == "items" ? " AND warehouse=0 AND COALESCE(inwardrobe,0)=0 AND position BETWEEN 0 AND 40" : "";
            string order = table == "items" ? "uid" : "id";
            var rows = new List<string>();
            using (var q = Q.Command(c, t, "SELECT row_to_json(r)::text FROM (SELECT * FROM " + table
                + " WHERE entityid=@uid" + filter + " ORDER BY " + order + " FOR UPDATE) r", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) { while (r.Read()) { if (rows.Count >= 512) throw new InvalidOperationException("Rebirth snapshot exceeds its bound."); rows.Add(r.GetString(0)); } }
            return string.Join("\n", rows);
        }
        private static void Ready(GameState p, Offer offer, Npcs dialog) {
            if (!p.Inventory.Objects.Any(Stone)) { dialog.Text("Bring an unlocked Heavenly Stone in your bag."); return; }
            offer.Cache = Cache(p);
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p); offer.Items = Rows(c, t, p, "items"); offer.Skills = Rows(c, t, p, "skills"); offer.Profs = Rows(c, t, p, "profs");
                AssertLoaded(c, t, p); t.Commit();
            }}
            offer.Stage = 4;
            dialog.Text("One Heavenly Stone ends this life. Rise at level 15 as a " + RebirthRules.Names[Array.IndexOf(RebirthRules.Classes, offer.Class)]
                + (offer.Bless ? ", with one unblessed item blessed." : ", with a random super gem."));
            if (p.Inventory.Objects.Any(i => Stone(i) && i.Bound)) dialog.Text(offer.Bless ? "A bound stone also binds the blessed item." : "A bound stone gives bound gifts.");
            dialog.Text("A permanent, unbound five-star Armor of Holy Light garment will mark your new life.");
            dialog.Option("Begin my new life.", 214);
        }
        internal static void AssertLoaded(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            foreach (string table in new[] { "skills", "profs" }) {
                var saved = new List<ushort>();
                using (var q = Q.Command(c, t, "SELECT id FROM " + table + " WHERE entityid=@uid", "uid", (long)p.Entity.UID))
                using (var r = q.ExecuteReader()) while (r.Read()) {
                    ushort id = checked((ushort)Convert.ToInt32(r[0]));
                    if (table != "skills" || !SkillTable.DerivedRuneSkill(id)) saved.Add(id);
                }
                var expected = table == "skills" ? p.Spells.Values.Where(s => s.Available && !SkillTable.DerivedRuneSkill(s.ID)).Select(s => s.ID)
                    : p.Proficiencies.Values.Where(s => s.Available).Select(s => s.ID);
                if (!saved.OrderBy(id => id).SequenceEqual(expected.OrderBy(id => id))) throw new InvalidOperationException("Rebirth skills or proficiencies require reload.");
            }
            var equipped = new List<uint>();
            using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND warehouse=0 AND position BETWEEN 1 AND 40", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) while (r.Read()) equipped.Add(checked((uint)Convert.ToInt64(r[0])));
            if (!equipped.OrderBy(id => id).SequenceEqual(p.Equipment.GetItems().Select(i => i.UID).OrderBy(id => id)))
                throw new InvalidOperationException("Rebirth equipment requires reload.");
            var bag = new List<uint>();
            using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND warehouse=0 AND position=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) while (r.Read()) bag.Add(checked((uint)Convert.ToInt64(r[0])));
            if (!bag.OrderBy(id => id).SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(id => id)))
                throw new InvalidOperationException("Rebirth inventory requires reload.");
        }
        internal static uint Lower(ConquerItem item) {
            byte slot = (byte)(item.Position > 20 ? item.Position - 20 : item.Position);
            if (slot < 1 || slot > 8 || slot == 7 || ItemHandler.IsArrow(item.ID)) return item.ID;
            var current = ConquerItemInformation.BaseInformations[item.ID];
            int target = slot == 8 ? 10 : item.ID / 1000 == 900 ? 40 : item.ID / 1000 == 500 ? 20 : 15;
            if (current.Level <= target) return item.ID;
            var next = ConquerItemInformation.BaseInformations.Values.Where(i => i.ID / 1000 == item.ID / 1000 && i.ID % 10 == item.ID % 10
                && i.Level <= current.Level && i.Level >= target).OrderBy(i => i.Level).ThenBy(i => i.ID).FirstOrDefault();
            if (next == null) throw new InvalidOperationException("Rebirth equipment grade missing.");
            return next.ID;
        }
        private static void Complete(GameState p, Offer offer, Npcs dialog) {
            offers.Remove(p);
            if (offer.Stage != 4 || offer.Cache != Cache(p)) { dialog.Text("Your belongings have changed. Let us begin again."); return; }
            var stone = p.Inventory.Objects.Where(Stone).OrderByDescending(i => i.Bound).ThenBy(i => i.UID).FirstOrDefault();
            if (stone == null) return;
            var gear = p.Equipment.GetItems().Select(i => new Gear { Item = i, Type = Lower(i), Position = i.Position, Bless = i.Bless, Bound = i.Bound }).ToArray();
            var blessable = gear.Where(g => g.Position >= 1 && g.Position <= 8 && g.Position != 7 && g.Bless == 0 && !ItemHandler.IsArrow(g.Item.ID)).ToArray();
            if (offer.Bless && blessable.Length == 0) { dialog.Text("Wear an unblessed piece of equipment, or choose a super gem."); return; }
            Gear blessed = offer.Bless ? blessable[Roll(blessable.Length)] : null;
            if (blessed != null) { blessed.Bless = 1; blessed.Bound |= stone.Bound; }
            var added = new List<ConquerItem>();
            if (!offer.Bless) added.Add(Q.New(700003u + (uint)Roll(7) * 10, stone.Bound));
            // Retain this edition's catalog-backed reward weapons for its supported original classes.
            uint weapon = offer.Class == 11 || offer.Class == 21 || offer.Class == 71 ? 410077u : offer.Class == 41 ? 500057u
                : offer.Class == 132 || offer.Class == 142 ? 421077u : 0;
            if (weapon != 0) { var item = Q.New(weapon, stone.Bound); item.Effect = offer.Class == 41 ? Enums.ItemEffect.Shield
                : offer.Class == 132 ? Enums.ItemEffect.MP : offer.Class == 142 ? Enums.ItemEffect.HP : Enums.ItemEffect.Poison; added.Add(item); }
            var garment=Q.New(187405,false);added.Add(garment);
            var hands = gear.Where(g => g.Position == 5 || g.Position == 25).ToArray();
            if (p.Inventory.Count - (stone.StackSize <= 1 ? 1 : 0) + hands.Length + added.Count > 40) { dialog.Text("Make room in your bag for your new life's gifts and off-hand equipment."); return; }
            var profile = CharacterProgression.PreserveEarnedAttributes(RebirthRules.Profile(p.Entity.Class, p.Entity.Level, offer.Class), CharacterProgression.Capture(p));
            var skills = RebirthSkillRules.Plan(p, offer.Class);
            var profs = p.Proficiencies.Values.Select(s => new Proficiency(true) { ID = s.ID, PreviousLevel = Math.Max(s.Level, s.PreviousLevel), Level = 0, Experience = 0, Available = s.Available }).ToArray();
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p);
                if (Rows(c, t, p, "items") != offer.Items || Rows(c, t, p, "skills") != offer.Skills || Rows(c, t, p, "profs") != offer.Profs)
                    throw new InvalidOperationException("Durable rebirth belongings changed.");
                using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                    if (Convert.ToUInt32(q.ExecuteScalar()) != 1002) throw new InvalidOperationException("Rebirth owner moved.");
                AchievementCatalog.PreserveMasteries(c, t, p);
                CharacterProgression.Persist(c, t, p, profile);
                AppearanceState.Persist(c, t, p, offer.Body, p.Entity.Face, p.Entity.HairStyle);
                string debit = stone.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=721259 AND position=0 AND warehouse=0 AND COALESCE(stacksize,0)=@stack AND COALESCE(bound,0)=@bound AND COALESCE(locked,0)=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "item", (long)stone.UID, "uid", (long)p.Entity.UID, "stack", (int)stone.StackSize, "bound", stone.Bound ? 1 : 0) != 1)
                    throw new InvalidOperationException("Rebirth stone changed.");
                // Non-changing slots are already protected by the complete saved-row preimage.
                // In particular, a steed's wire Bless byte is its red colour, not stored blessing.
                foreach (var g in gear.Where(g => g.Type != g.Item.ID || g.Position == 5 || g.Position == 25 || ReferenceEquals(g, blessed)))
                    if (Q.Execute(c, t, "UPDATE items SET id=@type,position=@next,bless=@bless,bound=@bound WHERE uid=@item AND entityid=@uid AND id=@old AND position=@slot AND warehouse=0 AND COALESCE(bless,0)=@oldbless AND COALESCE(bound,0)=@oldbound",
                        "type", (int)g.Type, "next", g.Position == 5 || g.Position == 25 ? 0 : (int)g.Position, "bless", (int)g.Bless,
                        "bound", g.Bound ? 1 : 0, "oldbound", g.Item.Bound ? 1 : 0, "oldbless", (int)g.Item.Bless,
                        "item", (long)g.Item.UID, "uid", (long)p.Entity.UID, "old", (int)g.Item.ID, "slot", (int)g.Position) != 1)
                        throw new InvalidOperationException("Rebirth equipment changed.");
                foreach (var item in added)
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,effect)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@effect)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID,
                        "bound", item.Bound ? 1 : 0, "dur", (int)item.Durability, "max", (int)item.MaximDurability, "effect", (int)item.Effect) != 1)
                        throw new InvalidOperationException("Rebirth gift was not saved.");
                foreach (var old in p.Spells.Values.Where(s => !SkillTable.DerivedRuneSkill(s.ID) && !skills.Any(n => n.ID == s.ID)))
                    if (old.Available && Q.Execute(c, t, "DELETE FROM skills WHERE entityid=@uid AND id=@id", "uid", (long)p.Entity.UID, "id", (int)old.ID) != 1)
                        throw new InvalidOperationException("Rebirth skill removal failed.");
                foreach (var skill in skills) SkillTable.Persist(c, t, p, skill);
                foreach (var prof in profs) {
                    string sql = prof.Available ? "UPDATE profs SET level=0,previouslevel=@previous,experience=0 WHERE entityid=@uid AND id=@id"
                        : "INSERT INTO profs(entityid,id,level,previouslevel,experience) VALUES(@uid,@id,0,@previous,0)";
                    if (Q.Execute(c, t, sql, "uid", (long)p.Entity.UID, "id", (int)prof.ID, "previous", (int)prof.PreviousLevel) != 1)
                        throw new InvalidOperationException("Rebirth proficiency reset failed.");
                }
                if (Q.Execute(c, t, @"INSERT INTO revora_first_rebirths(uid,old_class,old_level,old_experience,new_class,body,stone_uid,bound,reward_item,blessed_item,garment_uid,created_at)
VALUES(@uid,@old,@level,@exp,@new,@body,@stone,@bound,@reward,@blessed,@garment,@created)", "created", Clock(), "uid", (long)p.Entity.UID,
                    "old", (int)p.Entity.Class, "level", (int)p.Entity.Level, "exp", checked((long)p.Entity.Experience), "new", (int)offer.Class, "body", (int)offer.Body,
                    "stone", (long)stone.UID, "bound", stone.Bound, "reward", offer.Bless ? 0L : (long)added[0].UID, "blessed", blessed == null ? 0L : (long)blessed.Item.UID, "garment", (long)garment.UID) != 1)
                    throw new InvalidOperationException("Rebirth receipt was not saved.");
                if (!ProgressionEvidence.Complete(c, t, p.Entity.UID, 11030, "first-rebirth-v1", "heavenly-stone", 1)
                    || !ProgressionEvidence.Complete(c, t, p.Entity.UID, 10315, "first-rebirth-v1", "first-life", 1))
                    throw new InvalidOperationException("First-rebirth achievement already exists or was not saved.");
                if (!Near(p) || Clock() >= offer.Until || offer.Cache != Cache(p)) throw new InvalidOperationException("Rebirth session changed.");
                t.Commit();
            }}
            Q.Install(p, stone.StackSize <= 1 ? new[] { stone.UID } : new uint[0], new ConquerItem[0]);
            if (stone.StackSize > 1) { stone.StackSize--; stone.Mode = Enums.ItemMode.Update; stone.Send(p); stone.Mode = Enums.ItemMode.Default; }
            foreach (var g in gear) {
                g.Item.ID = g.Type; g.Item.Bless = g.Bless; g.Item.Bound = g.Bound;
                if (g.Position == 5 || g.Position == 25) {
                    if (!p.Equipment.Remove(g.Position, true) || !p.Inventory.Add(g.Item, Enums.ItemUse.None)) throw new InvalidOperationException("Committed off-hand requires reload.");
                }
                g.Item.Mode = Enums.ItemMode.Update; g.Item.Send(p); g.Item.Mode = Enums.ItemMode.Default;
            }
            Q.Install(p, new uint[0], added);
            foreach (var old in p.Spells.Values.ToArray()) if (!skills.Any(s => s.ID == old.ID)) {
                p.Spells.Remove(old.ID); p.Send(new Data(true) { UID = p.Entity.UID, dwParam = old.ID, ID = 0x6d });
            }
            foreach (var s in skills) { s.Available = true; p.Spells[s.ID] = s; if (s.ID != 3060) s.Send(p); }
            foreach (var s in profs) { s.Available = true; p.Proficiencies[s.ID] = s; s.Send(p); }
            CharacterProgression.ApplyCommitted(p, profile); AppearanceState.ApplyCommitted(p, offer.Body, p.Entity.Face, p.Entity.HairStyle);
            RebirthSkillRules.ClearAssassinModes(p);RebirthSkillRules.ClearNinjaModes(p);RebirthSkillRules.ClearMonkModes(p);RebirthSkillRules.ClearPirateModes(p);WarriorModes.Clear(p);
            p.LoadItemStats(); p.CalculateStatBonus(); p.CalculateHPBonus(); p.GemAlgorithm(); p.Equipment.UpdateEntityPacket();
            PathHuntSystem.SyncQuestList(p); SoulTrialSystem.SyncQuestList(p); CourierLoopSystem.SyncQuestList(p); Q.Refresh(p);
            dialog.Text("Your former life is remembered. Walk your new path well.");
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ItemSyncRoot == null) return false;
            bool handled = p.ActiveNpc == HeavenlyStoneCrafting.Sereth && (request.OptionID == 1 || request.OptionID >= 200 && request.OptionID <= 214);
            lock (p.ItemSyncRoot) {
                if (!handled) { offers.Remove(p); return false; }
                if (request.NpcID != 0 && request.NpcID != HeavenlyStoneCrafting.Sereth || !Near(p)) { offers.Remove(p); return true; }
                try {
                    var dialog = new Npcs(p); dialog.Avatar(15); Offer offer;
                    if (!RebirthRules.Eligible(p.Entity.Class, p.Entity.Level, p.Entity.Reborn)) {
                        offers.Remove(p); dialog.Text("Reach your highest rank and level 120. Water Saints may return at level 110. This rite is for your first rebirth.");
                    } else if (request.OptionID == 1) {
                        offers.Remove(p); offers.Add(p, new Offer { Stage = 1, Until = Clock().AddMinutes(2) });
                        dialog.Text("A Heavenly Stone begins a new life at level 15. Which path calls to you?");
                        for (int i = 0; i < RebirthRules.Classes.Length; i++) dialog.Option(RebirthRules.Names[i] + ".", (byte)(200 + i));
                    } else if (!offers.TryGetValue(p, out offer) || Clock() >= offer.Until) {
                        offers.Remove(p); dialog.Text("Let us speak of your first rebirth again.");
                    } else if (offer.Stage == 1 && request.OptionID >= 200 && request.OptionID <= 209) {
                        offer.Class = RebirthRules.Classes[request.OptionID - 200]; offer.Stage = 2;
                        dialog.Text("Choose the form of your new life."); dialog.Option("Slender.", 210); dialog.Option("Sturdy.", 211);
                    } else if (offer.Stage == 2 && (request.OptionID == 210 || request.OptionID == 211)) {
                        offer.Body = RebirthRules.SelectedBody(p.Entity.Body, request.OptionID == 211);
                        if (offer.Body == 0) throw new InvalidOperationException("Unknown rebirth body.");
                        offer.Stage = 3; dialog.Text("Take a super gem, or let fortune bless one unblessed piece you wear.");
                        dialog.Option("A random super gem.", 212); dialog.Option("Bless my equipment.", 213);
                    } else if (offer.Stage == 3 && (request.OptionID == 212 || request.OptionID == 213)) {
                        offer.Bless = request.OptionID == 213; Ready(p, offer, dialog);
                    } else if (request.OptionID == 214) Complete(p, offer, dialog);
                    else { offers.Remove(p); dialog.Text("Let us begin again."); }
                    dialog.Option("Farewell.", 255); dialog.Send();
                } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "first-rebirth", ex); }
            }
            return true;
        }
    }
}
