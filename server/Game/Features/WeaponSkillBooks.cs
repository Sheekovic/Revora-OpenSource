using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WeaponSkillBooks
    {
        internal static bool IsBook(uint id) { return Book(id) != null; }
        private sealed class Rule
        {
            internal readonly ushort Skill;
            internal readonly byte Level, Proficiency;
            internal readonly string Name;
            internal Rule(ushort skill, string name, byte level = 0, byte proficiency = 0)
            { Skill = skill; Name = name; Level = level; Proficiency = proficiency; }
        }
        private static Rule Book(uint item)
        {
            switch (item)
            {
                case 725005: return new Rule(1045, "Fast Blade", 40, 5);
                case 725010: return new Rule(1046, "Scent Sword", 40, 5);
                case 725011: return new Rule(1250, "Wide Strike", 0, 4);
                case 725012: return new Rule(1260, "Speed Gun", 0, 4);
                case 725013: return new Rule(1290, "Penetration", 0, 4);
                case 725014: return new Rule(1300, "Halt", 0, 4);
                case 725026: return new Rule(5010, "Snow", 0, 4);
                case 725027: return new Rule(5020, "Stranded Monster", 0, 4);
                case 725029: return new Rule(5030, "Phoenix", 30);
                case 725030: return new Rule(5040, "Boom", 0, 4);
                case 725031: return new Rule(5050, "Boreas", 0, 4);
                // The published pharmacist table omits these five. Preserve
                // Revora's existing unrestricted learning, without inventing a gate.
                case 725040: return new Rule(7000, "Seizer");
                case 725041: return new Rule(7010, "Earthquake");
                case 725042: return new Rule(7020, "Rage");
                case 725043: return new Rule(7030, "Celestial");
                case 725044: return new Rule(7040, "Roamer");
                default: return null;
            }
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Spells != null && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        private static bool Eligible(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Rule rule, SpellInformation info)
        {
            using (var q = Q.Command(c, t, "SELECT level FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                if (Convert.ToInt32(q.ExecuteScalar()) < rule.Level) return false;
            if (rule.Proficiency == 0) return true;
            if (p.Proficiencies == null || info.WeaponSubtype == null) return false;
            foreach (ushort id in info.WeaponSubtype.Distinct())
            {
                IProf prof;
                if (!p.Proficiencies.TryGetValue(id, out prof) || prof == null || prof.ID != id || prof.Level < rule.Proficiency) continue;
                using (var q = Q.Command(c, t, @"SELECT level FROM profs WHERE entityid=@uid AND id=@id FOR SHARE",
                    "uid", (long)p.Entity.UID, "id", (int)id))
                using (var r = q.ExecuteReader())
                    if (r.Read())
                    {
                        bool qualifies = !r.IsDBNull(0) && Convert.ToInt32(r.GetValue(0)) >= rule.Proficiency;
                        if (r.Read()) throw new InvalidOperationException("Duplicate weapon proficiency ownership.");
                        if (qualifies) return true;
                    }
            }
            return false;
        }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            Rule rule = item == null ? null : Book(item.ID);
            if (rule == null) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) try
            {
                ConquerItem owned;
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item)
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.InWardrobe
                    || (item.Days != 0 && DateTime.Now >= item.DayStamp.AddDays(item.Days))) return true;
                if (p.Spells.ContainsKey(rule.Skill)) { p.Send("You already know " + rule.Name + ". Keep the book."); return true; }
                if (p.Entity.Level < rule.Level) { p.Send("Reach level " + rule.Level + " to learn " + rule.Name + "."); return true; }
                SafeDictionary<byte, SpellInformation> levels; SpellInformation info;
                var terminal = new Spell(true) { ID = rule.Skill, Level = (byte)(rule.Skill == 1045 || rule.Skill == 1046 ? 4 : 9) };
                if (!WeaponSkillMastery.Fixed(terminal) || !SpellTable.SpellInformations.TryGetValue(rule.Skill, out levels)
                    || !levels.TryGetValue(0, out info)) throw new InvalidOperationException("Weapon book catalog changed.");
                if (rule.Proficiency != 0 && (p.Proficiencies == null || info.WeaponSubtype == null
                    || !info.WeaponSubtype.Any(id => p.Proficiencies.ContainsKey(id) && p.Proficiencies[id] != null
                        && p.Proficiencies[id].ID == id && p.Proficiencies[id].Level >= rule.Proficiency)))
                { p.Send("Train your weapon proficiency to level " + rule.Proficiency + " to learn " + rule.Name + "."); return true; }
                var skill = new Spell(true) { ID = rule.Skill, Level = 0 };
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        if (!Eligible(c, t, p, rule, info))
                        {
                            p.Send("Let your training settle, then try again.");
                            return true;
                        }
                        SkillTable.Persist(c, t, p, skill);
                        string sql = item.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                        if (Q.Execute(c, t, sql + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                            "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                            "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Weapon book ownership changed.");
                        t.Commit();
                    }
                }
                Q.Install(p, item.StackSize <= 1 ? new[] { item.UID } : new uint[0], new ConquerItem[0]);
                if (item.StackSize > 1) { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); }
                skill.Available = true;
                if (!p.AddSpell(skill)) throw new InvalidOperationException("Committed weapon skill requires reload.");
                p.Send("You have learned " + rule.Name + ".");
            }
            catch (Exception ex)
            {
                var databaseError = ex as PostgresException;
                if (databaseError != null) Console.WriteLine("[weapon-book] sqlstate=" + databaseError.SqlState);
                Q.Fail(p, "weapon-skill-book", ex);
            }
            return true;
        }
    }
}
