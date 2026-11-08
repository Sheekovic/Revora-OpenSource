using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TrojanEpicTraining
    {
        private static readonly ushort[] arts = { 11960, 11970, 11980, 11990 };
        private sealed class Offer { internal ushort[] Arts; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        private static bool enabled;
        internal static void Initialize()
        {
            foreach (ushort id in arts) if (SpellTable.GetSpell(id, 0) == null) throw new InvalidOperationException("Trojan epic training catalog missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_trojan_epic_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,spell integer NOT NULL CHECK(spell IN(11960,11970,11980,11990)),
completed integer NOT NULL CHECK(completed>=CASE WHEN spell=11960 THEN 1 WHEN spell=11970 THEN 7 ELSE 0 END),
class integer NOT NULL CHECK(class BETWEEN 10 AND 15),level integer NOT NULL,reborn integer NOT NULL,
created_at timestamptz NOT NULL DEFAULT now());"); }
            enabled = true;
        }
        private static bool Near(GameState p)
        {
            return Q.Near(p, 10022, 1004) && p.Map.BaseID == 1004 && CharacterProgression.Managed(p)
                && p.Entity.PokerTable == 0 && p.Entity.Class >= 10 && p.Entity.Class <= 15;
        }
        private static bool Earned(ushort id, int completed) { return completed >= (id == 11960 ? 1 : id == 11970 ? 7 : 0); }
        private static int Completions(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            int completed;
            using (var q = Q.Command(c, t, "SELECT completed FROM revora_trojan_epic WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                completed = Convert.ToInt32(q.ExecuteScalar());
            using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_trojan_epic_rewards WHERE uid=@uid", "uid", (long)p.Entity.UID))
                if (Convert.ToInt32(q.ExecuteScalar()) != completed) throw new InvalidOperationException("Trojan epic completion receipts disagree.");
            return completed;
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (p == null || request == null || p.ActiveNpc != 10022 || (request.OptionID != 49 && request.OptionID != 50 && request.OptionID != 84)) return false;
            if (p.ItemSyncRoot == null || request.NpcID != 0 && request.NpcID != 10022) return true;
            lock (p.ItemSyncRoot) try {
                if (!Q.Near(p, 10022, 1004)) return true;
                var d = new Npcs(p);
                if (!enabled || !TrojanEpicQuest.Enabled) d.Text("The ancient path is not ready. Return another day.");
                else if (!Near(p)) d.Text("These arts belong to the Trojan. Seek your own master.");
                else if (request.OptionID == 50) {
                    offers.Remove(p);
                    if (!TrojanEpicStore.Eligible(p)) d.Text("Reach level 120, or be reborn, before seeking the Solar Blade.");
                    else {
                        p.Entity.Teleport(1002, 425, 302);
                        if (p.Entity.MapID != 1002 || !EntityTable.SaveEntity(p)) throw new InvalidOperationException("Trojan quest referral was not saved.");
                        p.Send("Speak with Pak's Ghost nearby. He remembers the Solar Blade."); return true;
                    }
                } else if (request.OptionID == 49) {
                    offers.Remove(p); int completed = TrojanEpicStore.Load(p).Completed;
                    var ready = arts.Where(id => Earned(id, completed) && p.Entity.Level >= SpellTable.GetSpell(id, 0).NeedLevel && !p.Spells.ContainsKey(id)).ToArray();
                    d.Text("Breath Focus follows one completed journey; Super Cyclone follows seven. Fatal Cross and Mortal Strike need two Cross Sabers in battle.");
                    if (ready.Length != 0) {
                        offers.Add(p, new Offer { Arts = ready, Until = TrojanEpicScenes.Clock().AddMinutes(2) });
                        d.Option("Teach me the arts I have earned.", 84);
                    }
                } else {
                    Offer offer;
                    if (!offers.TryGetValue(p, out offer) || TrojanEpicScenes.Clock() >= offer.Until) return true;
                    offers.Remove(p); Learn(p, offer.Arts, d);
                }
                d.Option("Farewell.", 255); d.Send();
            } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "trojan-epic-training", ex); }
            return true;
        }
        private static void Learn(GameState p, ushort[] offered, Npcs d)
        {
            var profile = CharacterProgression.Capture(p); Spell[] skills;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p); CharacterProgression.Persist(c, t, p, profile);
                int completed = Completions(c, t, p);
                skills = offered.Where(id => Earned(id, completed) && p.Entity.Level >= SpellTable.GetSpell(id, 0).NeedLevel && !p.Spells.ContainsKey(id))
                    .Select(id => new Spell(true) { ID = id, Level = 0, PreviousLevel = 0, Experience = 0 }).ToArray();
                foreach (var skill in skills) {
                    SkillTable.Persist(c, t, p, skill);
                    if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_training(id,uid,spell,completed,class,level,reborn)
VALUES(@id,@uid,@spell,@completed,@class,@level,@reborn)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID, "spell", (int)skill.ID,
                        "completed", completed, "class", (int)p.Entity.Class, "level", (int)p.Entity.Level, "reborn", (int)p.Entity.Reborn) != 1)
                        throw new InvalidOperationException("Trojan training receipt missing.");
                }
                if (!Near(p) || skills.Any(s => p.Spells.ContainsKey(s.ID))) throw new InvalidOperationException("Trojan training owner changed.");
                t.Commit();
            }}
            CharacterProgression.Committed(p, profile);
            foreach (var skill in skills) { skill.Available = true; p.Spells.Add(skill.ID, skill); skill.Send(p); }
            d.Text(skills.Length == 0 ? "No new lesson awaits you yet." : "The lesson is complete. Put these arts to honorable use.");
        }
        internal static bool CanPerform(GameState p, ushort id)
        {
            if (id != 11980 && id != 11990) return true;
            if (p == null || p.Entity == null || p.Entity.Class < 10 || p.Entity.Class > 15 || p.Equipment == null) return false;
            var right = p.Equipment.TryGetItem((byte)(p.AlternateEquipment ? 24 : 4));
            var left = p.Equipment.TryGetItem((byte)(p.AlternateEquipment ? 25 : 5));
            return right != null && left != null && right.UID != left.UID && right.ID / 1000 == 614 && left.ID / 1000 == 614;
        }
    }
}
