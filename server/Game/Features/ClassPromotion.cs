using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Trainer promotions only: class changes and rebirths cannot create these receipts.
    internal static class ClassPromotion
    {
        private sealed class Offer { internal uint Trainer; internal byte Class, Reborn; internal DateTime Until; }
        private sealed class Debit { internal ConquerItem Item; internal ushort Before, After; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static byte School(uint trainer) {
            switch (trainer) {
                case 10022: return 10; case 10001: return 20; case 400: return 40; case 4720: return 50;
                case 4271: return 60; case 4272: return 70; case 5061: return 80; case 10000: return 100;
                case 51580: return 160; default: return 0;
            }
        }
        private static bool Student(byte school, byte profession) {
            return school == 100 ? profession == 100 || profession == 101 || profession >= 132 && profession <= 135
                || profession >= 142 && profession <= 145 : profession >= school && profession <= school + 5;
        }
        private static bool MoonBox(uint id) {
            return id >= 721020 && id <= 721025 || id >= 721030 && id <= 721035 || id >= 721040 && id <= 721043
                || id >= 721051 && id <= 721055 || id >= 721061 && id <= 721065 || id >= 721080 && id <= 721084 || id == 721090;
        }
        private static bool Usable(ConquerItem item) {
            return item.Position == 0 && item.Warehouse == 0 && item.Lock == 0 && !item.Suspicious && !item.InWardrobe
                && item.Days == 0 && item.TimeLeftInMinutes == 0;
        }
        private static List<Debit> Materials(GameState p) {
            int count = p.PromoteItemCountNeed; uint type = p.PromoteItemNeed; var result = new List<Debit>();
            if (count == 0) return result;
            foreach (var item in p.Inventory.Objects.Where(i => Usable(i) && (type == 721020 ? MoonBox(i.ID) : i.ID == type)).OrderBy(i => i.UID)) {
                int amount = Math.Max(1, (int)item.StackSize), take = Math.Min(count, amount);
                result.Add(new Debit { Item = item, Before = item.StackSize, After = (ushort)(amount - take) });
                count -= take; if (count == 0) return result;
            }
            return null;
        }
        private static void WriteDebit(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Debit debit) {
            var item = debit.Item; ConquerItem owned;
            if (!p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item) || !Usable(item) || item.StackSize != debit.Before)
                throw new InvalidOperationException("Promotion material changed.");
            string sql = debit.After == 0 ? "DELETE FROM items" : "UPDATE items SET stacksize=@after";
            sql += @" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before
AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'";
            if (Q.Execute(c, t, sql, "after", (int)debit.After, "item", (long)item.UID, "uid", (long)p.Entity.UID,
                "type", (int)item.ID, "before", (int)debit.Before, "bound", item.Bound ? 1 : 0) != 1)
                throw new InvalidOperationException("Saved promotion material changed.");
        }
        private static void Receipt(NpgsqlConnection c, NpgsqlTransaction t, uint uid, int id, bool inserted) {
            if (inserted) return;
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id)",
                "uid", (long)uid, "id", id))
                if (!Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Promotion receipt not saved.");
        }
        private static void Record(NpgsqlConnection c, NpgsqlTransaction t, uint uid, byte profession) {
            const string source = "class-promotion-v1"; string key = "class-" + profession;
            switch (profession) {
                case 11:
                    Receipt(c, t, uid, 10819, ProgressionEvidence.Complete(c, t, uid, 10819, source, key, 1));
                    break;
                case 12:
                    Receipt(c, t, uid, 10820, ProgressionEvidence.Complete(c, t, uid, 10820, source, key, 1));
                    break;
                case 13:
                    Receipt(c, t, uid, 10821, ProgressionEvidence.Complete(c, t, uid, 10821, source, key, 1));
                    break;
                case 14:
                    Receipt(c, t, uid, 10822, ProgressionEvidence.Complete(c, t, uid, 10822, source, key, 1));
                    break;
                case 15:
                    Receipt(c, t, uid, 10823, ProgressionEvidence.Complete(c, t, uid, 10823, source, key, 1));
                    break;
                case 21:
                    Receipt(c, t, uid, 10813, ProgressionEvidence.Complete(c, t, uid, 10813, source, key, 1));
                    break;
                case 22:
                    Receipt(c, t, uid, 10814, ProgressionEvidence.Complete(c, t, uid, 10814, source, key, 1));
                    break;
                case 23:
                    Receipt(c, t, uid, 10815, ProgressionEvidence.Complete(c, t, uid, 10815, source, key, 1));
                    break;
                case 24:
                    Receipt(c, t, uid, 10816, ProgressionEvidence.Complete(c, t, uid, 10816, source, key, 1));
                    break;
                case 25:
                    Receipt(c, t, uid, 10817, ProgressionEvidence.Complete(c, t, uid, 10817, source, key, 1));
                    break;
                case 41:
                    Receipt(c, t, uid, 10903, ProgressionEvidence.Complete(c, t, uid, 10903, source, key, 1));
                    break;
                case 42:
                    Receipt(c, t, uid, 10904, ProgressionEvidence.Complete(c, t, uid, 10904, source, key, 1));
                    break;
                case 43:
                    Receipt(c, t, uid, 10905, ProgressionEvidence.Complete(c, t, uid, 10905, source, key, 1));
                    break;
                case 44:
                    Receipt(c, t, uid, 10906, ProgressionEvidence.Complete(c, t, uid, 10906, source, key, 1));
                    break;
                case 45:
                    Receipt(c, t, uid, 10907, ProgressionEvidence.Complete(c, t, uid, 10907, source, key, 1));
                    break;
                case 51:
                    Receipt(c, t, uid, 10908, ProgressionEvidence.Complete(c, t, uid, 10908, source, key, 1));
                    break;
                case 52:
                    Receipt(c, t, uid, 10909, ProgressionEvidence.Complete(c, t, uid, 10909, source, key, 1));
                    break;
                case 53:
                    Receipt(c, t, uid, 10910, ProgressionEvidence.Complete(c, t, uid, 10910, source, key, 1));
                    break;
                case 54:
                    Receipt(c, t, uid, 10911, ProgressionEvidence.Complete(c, t, uid, 10911, source, key, 1));
                    break;
                case 55:
                    Receipt(c, t, uid, 10912, ProgressionEvidence.Complete(c, t, uid, 10912, source, key, 1));
                    break;
                case 61:
                    Receipt(c, t, uid, 10913, ProgressionEvidence.Complete(c, t, uid, 10913, source, key, 1));
                    break;
                case 62:
                    Receipt(c, t, uid, 10914, ProgressionEvidence.Complete(c, t, uid, 10914, source, key, 1));
                    break;
                case 63:
                    Receipt(c, t, uid, 10915, ProgressionEvidence.Complete(c, t, uid, 10915, source, key, 1));
                    break;
                case 64:
                    Receipt(c, t, uid, 10916, ProgressionEvidence.Complete(c, t, uid, 10916, source, key, 1));
                    break;
                case 65:
                    Receipt(c, t, uid, 10917, ProgressionEvidence.Complete(c, t, uid, 10917, source, key, 1));
                    break;
                case 71:
                    Receipt(c, t, uid, 11205, ProgressionEvidence.Complete(c, t, uid, 11205, source, key, 1));
                    break;
                case 72:
                    Receipt(c, t, uid, 11206, ProgressionEvidence.Complete(c, t, uid, 11206, source, key, 1));
                    break;
                case 73:
                    Receipt(c, t, uid, 11207, ProgressionEvidence.Complete(c, t, uid, 11207, source, key, 1));
                    break;
                case 74:
                    Receipt(c, t, uid, 11208, ProgressionEvidence.Complete(c, t, uid, 11208, source, key, 1));
                    break;
                case 75:
                    Receipt(c, t, uid, 11209, ProgressionEvidence.Complete(c, t, uid, 11209, source, key, 1));
                    break;
                case 81:
                    Receipt(c, t, uid, 11216, ProgressionEvidence.Complete(c, t, uid, 11216, source, key, 1));
                    break;
                case 82:
                    Receipt(c, t, uid, 11217, ProgressionEvidence.Complete(c, t, uid, 11217, source, key, 1));
                    break;
                case 83:
                    Receipt(c, t, uid, 11218, ProgressionEvidence.Complete(c, t, uid, 11218, source, key, 1));
                    break;
                case 84:
                    Receipt(c, t, uid, 11219, ProgressionEvidence.Complete(c, t, uid, 11219, source, key, 1));
                    break;
                case 85:
                    Receipt(c, t, uid, 11220, ProgressionEvidence.Complete(c, t, uid, 11220, source, key, 1));
                    break;
                case 101:
                    Receipt(c, t, uid, 10824, ProgressionEvidence.Complete(c, t, uid, 10824, source, key, 1));
                    Receipt(c, t, uid, 10829, ProgressionEvidence.Complete(c, t, uid, 10829, source, key, 1));
                    break;
                case 132:
                    Receipt(c, t, uid, 10830, ProgressionEvidence.Complete(c, t, uid, 10830, source, key, 1));
                    break;
                case 133:
                    Receipt(c, t, uid, 10831, ProgressionEvidence.Complete(c, t, uid, 10831, source, key, 1));
                    break;
                case 134:
                    Receipt(c, t, uid, 10832, ProgressionEvidence.Complete(c, t, uid, 10832, source, key, 1));
                    break;
                case 135:
                    Receipt(c, t, uid, 10901, ProgressionEvidence.Complete(c, t, uid, 10901, source, key, 1));
                    break;
                case 142:
                    Receipt(c, t, uid, 10825, ProgressionEvidence.Complete(c, t, uid, 10825, source, key, 1));
                    break;
                case 143:
                    Receipt(c, t, uid, 10826, ProgressionEvidence.Complete(c, t, uid, 10826, source, key, 1));
                    break;
                case 144:
                    Receipt(c, t, uid, 10827, ProgressionEvidence.Complete(c, t, uid, 10827, source, key, 1));
                    break;
                case 145:
                    Receipt(c, t, uid, 10828, ProgressionEvidence.Complete(c, t, uid, 10828, source, key, 1));
                    break;
                case 161:
                    Receipt(c, t, uid, 11304, ProgressionEvidence.Complete(c, t, uid, 11304, source, key, 1));
                    break;
                case 162:
                    Receipt(c, t, uid, 11305, ProgressionEvidence.Complete(c, t, uid, 11305, source, key, 1));
                    break;
                case 163:
                    Receipt(c, t, uid, 11306, ProgressionEvidence.Complete(c, t, uid, 11306, source, key, 1));
                    break;
                case 164:
                    Receipt(c, t, uid, 11307, ProgressionEvidence.Complete(c, t, uid, 11307, source, key, 1));
                    break;
                case 165:
                    Receipt(c, t, uid, 11308, ProgressionEvidence.Complete(c, t, uid, 11308, source, key, 1));
                    break;
                default: throw new InvalidOperationException("Unknown promotion destination.");
            }
        }
        private static void Prepare(GameState p, uint trainer, Npcs dialog) {
            offers.Remove(p);
            if (!Student(School(trainer), p.Entity.Class)) { dialog.Text("Seek the master of your own school."); return; }
            if (p.Entity.Class % 10 == 5) { dialog.Text("You have reached the highest rank in our school."); return; }
            string material = p.PromoteItemNeed == 0 ? "" : p.PromoteItemNeed == 1072031 ? " and five Euxenite Ores"
                : p.PromoteItemNeed == 1080001 ? " and an Emerald" : p.PromoteItemNeed == 1088001 ? " and a Meteor" : " and a Moon Box";
            dialog.Text("Reach level " + p.PromoteLevelNeed + material + ", and I will advance your rank.");
            offers.Add(p, new Offer { Trainer = trainer, Class = p.Entity.Class, Reborn = p.Entity.Reborn, Until = Clock().AddMinutes(2) });
            if (p.Entity.Class == 101) { dialog.Option("Follow Water.", 253); dialog.Option("Follow Fire.", 254); }
            else dialog.Option("I am ready.", trainer == 51580 ? (byte)2 : p.Entity.Class == 100 ? (byte)254 : (byte)3);
        }
        private static void Promote(GameState p, uint trainer, byte option, Npcs dialog) {
            Offer offer;
            if (!offers.TryGetValue(p, out offer)) { dialog.Text("Let us discuss your next rank first."); return; }
            offers.Remove(p);
            if (offer.Trainer != trainer || offer.Class != p.Entity.Class || offer.Reborn != p.Entity.Reborn || Clock() >= offer.Until
                || !Student(School(trainer), p.Entity.Class) || p.Entity.Class % 10 == 5) { dialog.Text("Let us discuss your next rank again."); return; }
            bool taoist = p.Entity.Class == 100 || p.Entity.Class == 101;
            if (taoist ? option != 253 && option != 254 : option != (trainer == 51580 ? 2 : 3)) return;
            if (p.Entity.Level < p.PromoteLevelNeed) { dialog.Text("Train a little longer. Your next rank awaits at level " + p.PromoteLevelNeed + "."); return; }
            var debits = Materials(p);
            if (debits == null) { dialog.Text("Bring the required offering in your bag, unlocked."); return; }
            uint rewardId = p.PromoteItemGain;
            if (rewardId != 0 && p.Inventory.Count - debits.Count(d => d.After == 0) >= 40) { dialog.Text("Make room in your bag for your promotion reward."); return; }
            byte next = p.Entity.Class == 101 ? (option == 253 ? (byte)132 : (byte)142) : (byte)(p.Entity.Class + 1);
            var profile = CharacterProgression.Capture(p); profile.Values[7] = next;
            var rewards = rewardId == 0 ? new ConquerItem[0] : new[] { Q.New(rewardId, debits.Any(d => d.Item.Bound)) };
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                CharacterProgression.Persist(c, t, p, profile);
                using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Promotion inventory changed.");
                foreach (var debit in debits) WriteDebit(c, t, p, debit);
                foreach (var reward in rewards)
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)", "item", (long)reward.UID, "type", (int)reward.ID, "uid", (long)p.Entity.UID,
                        "bound", reward.Bound ? 1 : 0, "dur", (int)reward.Durability, "max", (int)reward.MaximDurability) != 1)
                        throw new InvalidOperationException("Promotion reward not saved.");
                Record(c, t, p.Entity.UID, next);
                if (!Q.Near(p, trainer, 1004) || Clock() >= offer.Until || p.Entity.Class != offer.Class || p.Entity.Reborn != offer.Reborn)
                    throw new InvalidOperationException("Promotion session changed.");
                t.Commit();
            }}
            Q.Install(p, debits.Where(d => d.After == 0).Select(d => d.Item.UID), rewards);
            foreach (var debit in debits.Where(d => d.After != 0)) {
                debit.Item.StackSize = debit.After; debit.Item.Mode = Enums.ItemMode.Update; debit.Item.Send(p); debit.Item.Mode = Enums.ItemMode.Default;
            }
            CharacterProgression.ApplyCommitted(p, profile); Q.Refresh(p);
            p.Entity.Update(10, "end_task", true); dialog.Text("You have earned your new rank. Carry our school's honor with you.");
        }
        internal static void ObserveDialogue(GameState p, NpcRequest request) {
            if (p == null || p.ItemSyncRoot == null || request == null) return;
            lock (p.ItemSyncRoot) {
                Offer offer;
                if (offers.TryGetValue(p, out offer) && (request.OptionID == 0 || request.OptionID == 255
                    || p.ActiveNpc != offer.Trainer || request.NpcID != 0 && request.NpcID != offer.Trainer)) offers.Remove(p);
            }
        }
        internal static void Menu(GameState p, uint trainer, NpcRequest request, Npcs dialog) {
            if (request == null || request.NpcID != 0 && request.NpcID != trainer) return;
            byte option = request.OptionID;
            if (p == null || p.ItemSyncRoot == null || School(trainer) == 0) return;
            lock (p.ItemSyncRoot) {
                if (!Q.Near(p, trainer, 1004) || !CharacterProgression.Managed(p)) { offers.Remove(p); return; }
                try {
                    if (option == 1 || option == 100) Prepare(p, trainer, dialog); else Promote(p, trainer, option, dialog);
                    dialog.Option("Farewell.", 255); dialog.Send();
                } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "class-promotion", ex); }
            }
        }
    }
}
