namespace Revora.Database
{
    using Revora.Client;
    using Revora.Interfaces;
    using Revora.Network.GamePackets;
    using Npgsql;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Revora.Game.Features;

    public class SkillTable
    {
        #region Blob

        #region Main
        public void GetProf(System.IO.BinaryReader reader, out List<Network.GamePackets.Proficiency> profscolletion)
        {
            profscolletion = new List<Network.GamePackets.Proficiency>();
            uint count = reader.ReadUInt32();
            for (uint x = 0; x < count; x++)
            {
                Network.GamePackets.Proficiency proficiency = new Network.GamePackets.Proficiency(true);
                proficiency.ID = reader.ReadUInt16();
                proficiency.Level = reader.ReadByte();
                proficiency.PreviousLevel = reader.ReadByte();
                proficiency.Experience = reader.ReadUInt32();
                proficiency.NeededExperience = Database.DataHolder.ProficiencyLevelExperience(proficiency.Level);
                proficiency.Available = true;
                profscolletion.Add(proficiency);
            }
        }
        public void GetSkill(System.IO.BinaryReader reader, out List<Interfaces.ISkill> spellscollection)
        {
            spellscollection = new List<Interfaces.ISkill>();
            uint count = reader.ReadUInt32();
            for (uint x = 0; x < count; x++)
            {
                Interfaces.ISkill spell = new Network.GamePackets.Spell(true);
                spell.ID = reader.ReadUInt16();
                spell.Level = reader.ReadByte();
                spell.PreviousLevel = reader.ReadByte();
                spell.Experience = reader.ReadUInt32();
                spell.Souls = (Spell.Soul_Level)reader.ReadByte();
                spell.Available = true;
                spellscollection.Add(spell);
            }
        }

        #endregion

        #region other
        public static void WriteProf(System.IO.BinaryWriter writer, Interfaces.IProf proficiency)
        {
            writer.Write(proficiency.ID);
            writer.Write(proficiency.Level);
            writer.Write(proficiency.PreviousLevel);
            writer.Write(proficiency.Experience);

        }
        public static byte[] GetArrayProfs(Client.GameState client)
        {
            uint count = (uint)client.Proficiencies.Count;
            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);
            writer.Write(count);
            if (client.Proficiencies != null)
            {
                foreach (var prof in client.Proficiencies.Values)
                    WriteProf(writer, prof);
            }
            return stream.ToArray();
        }
        public static byte[] GetSpellsArray(Client.GameState client)
        {
            uint count = (uint)client.Spells.Count;
            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);
            writer.Write(count);
            foreach (var skill in client.Spells.Values)
                WriteSkill(writer, skill);
            return stream.ToArray();
        }
        public static void WriteSkill(System.IO.BinaryWriter writer, Interfaces.ISkill skill)
        {
            writer.Write(skill.ID);
            writer.Write(skill.Level);
            writer.Write(skill.PreviousLevel);
            writer.Write(skill.Experience);
            writer.Write((byte)skill.Souls);
        }

        #endregion

        #endregion
        public static void removeAllSkills(Client.GameState client)
        {
            if (client == null || client.Fake || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            LockOwner(c, t, client);
                            QuestInventory.Execute(c, t, "DELETE FROM skills WHERE entityid=@uid", "uid", (long)client.Entity.UID);
                            t.Commit();
                        }
                    }
                }
                catch (Exception ex) { QuestInventory.Fail(client, "skill-reset", ex); throw; }
            }
        }
        public static void removeAllProfs(Client.GameState client)
        {
            using (var conn = DataHolder.MySqlConnection)
            {
                using (var cmd = new Npgsql.NpgsqlCommand(Revora.Database.DataHolder.ToPostgres("Delete from profs where EntityID =" + client.Entity.UID.ToString()), conn))
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteSpell(GameState client, ushort ID)
        {
            if (client == null || client.Fake || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                try
                {
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            LockOwner(c, t, client);
                            QuestInventory.Execute(c, t, "DELETE FROM skills WHERE entityid=@uid AND id=@id",
                                "uid", (long)client.Entity.UID, "id", (int)ID);
                            t.Commit();
                        }
                    }
                }
                catch (Exception ex) { QuestInventory.Fail(client, "skill-delete", ex); throw; }
            }
        }

        public static void LoadProficiencies(GameState client)
        {
            if (client.Fake) return;
            if (client.Entity != null)
            {
                client.Proficiencies = new SafeDictionary<ushort, IProf>(100);
                MySqlReader reader = new MySqlReader(new Database.MySqlCommand(MySqlCommandType.SELECT).Select("profs").Where("EntityID", (long)client.Entity.UID));
                while (reader.Read())
                {
                    IProf prof = new Proficiency(true)
                    {
                        ID = reader.ReadUInt16("ID"),
                        Level = reader.ReadByte("Level"),
                        PreviousLevel = reader.ReadByte("PreviousLevel"),
                        Experience = reader.ReadUInt32("Experience"),
                        Available = true
                    };
                    if (!client.Proficiencies.ContainsKey(prof.ID))
                    {
                        client.Proficiencies.Add(prof.ID, prof);
                    }
                }
            }
        }

        public static void LoadProficiencies(GameState client, Npgsql.NpgsqlConnection conn)
        {
            LoadProficiencies(client);
        }

        public static void LoadSpells(GameState client)
        {
            if (client.Fake) return;
            if (client.Entity != null)
            {
                client.Spells = new SafeDictionary<ushort, ISkill>(100);
                MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("skills").Where("EntityID", (long)client.Entity.UID));
                while (reader.Read())
                {
                    byte savedLevel = reader.ReadByte("Level");
                    ISkill skill = new Spell(true)
                    {
                        ID = reader.ReadUInt16("ID"),
                        Level = savedLevel,
                        PreviousLevel = reader.ReadByte("PreviousLevel"),
                        Experience = reader.ReadUInt32("Experience"),
                        Souls = (Spell.Soul_Level)reader.ReadByte("LevelHu"),
                        LevelHu2 = reader.ReadByte("LevelHu2"),
                        Available = true
                    };
                    // Spell.Level clamps unknown levels. Do not turn corrupt saved
                    // levels into a valid terminal skill and a mastery achievement.
                    if (skill.Level == savedLevel && !DerivedRuneSkill(skill.ID) && !client.Spells.ContainsKey(skill.ID))
                    {
                        client.Spells.Add(skill.ID, skill);
                    }
                }
            }
        }

        public static void LoadSpells(GameState client, Npgsql.NpgsqlConnection conn)
        {
            LoadSpells(client);
        }

        public static void SaveProficiencies(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                if (client.Entity.PersistenceRequiresReload || client.Proficiencies == null || client.Proficiencies.Count == 0) return;
                var profs = client.Proficiencies.Values.ToArray();
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        LockOwner(c, t, client);
                        foreach (var prof in profs)
                        {
                            string sql = prof.Available
                                ? "UPDATE profs SET level=@level,previouslevel=@previous,experience=@exp WHERE entityid=@uid AND id=@id"
                                : @"INSERT INTO profs(entityid,id,level,previouslevel,experience)
SELECT @uid,@id,@level,@previous,@exp WHERE NOT EXISTS(SELECT 1 FROM profs WHERE entityid=@uid AND id=@id)";
                            if (QuestInventory.Execute(c, t, sql, "uid", (long)client.Entity.UID, "id", (int)prof.ID,
                                "level", (int)prof.Level, "previous", (int)prof.PreviousLevel, "exp", (long)prof.Experience) != 1)
                                throw new InvalidOperationException("Proficiency row ownership changed.");
                        }
                        t.Commit();
                    }
                }
                // A rejected transaction must leave new entries insertable on retry.
                foreach (var prof in profs) prof.Available = true;
            }
        }

        public static void SaveProficiencies(GameState client, Npgsql.NpgsqlConnection conn)
        {
            SaveProficiencies(client);
        }

        public static void SaveSpells(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null) return;
            lock (client.ItemSyncRoot ?? client)
            {
                if (client.Entity.PersistenceRequiresReload || client.Spells == null || client.Spells.Count == 0) return;
                try
                {
                    var skills = client.Spells.Values.Where(skill => !DerivedRuneSkill(skill.ID)).ToArray();
                    if (skills.Length == 0) return;
                    bool mastery = false;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            LockOwner(c, t, client);
                            foreach (var skill in skills)
                            {
                                Persist(c, t, client, skill);
                                mastery |= WeaponSkillMastery.Record(c, t, client, skill);
                            }
                            t.Commit();
                        }
                    }
                    foreach (var skill in skills) skill.Available = true;
                    if (mastery) QuestInventory.Refresh(client);
                }
                catch (Exception ex) { QuestInventory.Fail(client, "skill-save", ex); }
            }
        }
        internal static void LockOwner(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            if (t == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Skill persistence requires its character transaction.");
            using (var q = QuestInventory.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                if (q.ExecuteScalar() == null) throw new InvalidOperationException("Skill owner missing.");
        }
        internal static bool DerivedRuneSkill(ushort id)
        { return Revora.Network.GamePackets.RunesSystem.RunesSpells.Contains(id) || id >= 40100 && id < 40400; }
        internal static void Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ISkill skill)
        {
            if (t == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload || skill == null)
                throw new InvalidOperationException("Skill persistence is not ready.");
            string sql = skill.Available
                ? "UPDATE skills SET level=@level,previouslevel=@previous,experience=@exp,levelhu=@soul,levelhu2=@soul2 WHERE entityid=@uid AND id=@id"
                : @"INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2)
SELECT @uid,@id,@level,@previous,@exp,@soul,@soul2 WHERE NOT EXISTS(SELECT 1 FROM skills WHERE entityid=@uid AND id=@id)";
            if (QuestInventory.Execute(c, t, sql, "uid", (long)p.Entity.UID, "id", (int)skill.ID,
                "level", (int)skill.Level, "previous", (int)skill.PreviousLevel, "exp", (long)skill.Experience,
                "soul", (int)skill.Souls, "soul2", (int)skill.LevelHu2) != 1)
                throw new InvalidOperationException("Skill row ownership changed.");
        }

        public static void SaveSpells(GameState client, Npgsql.NpgsqlConnection conn)
        {
            SaveSpells(client);
        }
    }
}
