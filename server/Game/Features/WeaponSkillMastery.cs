using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;

namespace Revora.Game.Features
{
    internal static class WeaponSkillMastery
    {
        internal static bool Fixed(ISkill skill)
        {
            if (skill == null) return false;
            byte terminal;
            switch (skill.ID)
            {
                case 1045: case 1046: terminal = 4; break;
                case 5030: case 5040: case 1300: case 5020: case 1260: case 1290: case 5010: case 1250:
                case 5050: case 7000: case 7010: case 7020: case 7030: case 7040: terminal = 9; break;
                default: return false;
            }
            if (skill.Level != terminal) return false;
            SafeDictionary<byte, SpellInformation> levels;
            if (!SpellTable.SpellInformations.TryGetValue(skill.ID, out levels) || levels == null || levels.Count != terminal + 1) return false;
            for (byte level = 0; level <= terminal; level++)
            {
                SpellInformation info;
                if (!levels.TryGetValue(level, out info) || info == null || info.ID != skill.ID || info.Level != level
                    || (level == terminal ? info.NeedExperience != 0 : info.NeedExperience == 0)) return false;
            }
            return true;
        }
        internal static bool Record(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ISkill skill)
        {
            if (!QuestInventory.Player(p) || !Fixed(skill)) return false;
            ISkill owned;
            if (t == null || !Monitor.IsEntered(p.ItemSyncRoot) || p.Spells == null
                || !p.Spells.TryGetValue(skill.ID, out owned) || !ReferenceEquals(owned, skill))
                throw new InvalidOperationException("Weapon skill achievement requires owned skill persistence.");
            using (var q = QuestInventory.Command(c, t, @"SELECT count(*),count(*) FILTER(WHERE level=@level)
FROM skills WHERE entityid=@uid AND id=@id", "uid", (long)p.Entity.UID, "id", (int)skill.ID, "level", (int)skill.Level))
            using (var r = q.ExecuteReader())
                if (!r.Read() || r.GetInt64(0) != 1 || r.GetInt64(1) != 1)
                    throw new InvalidOperationException("Weapon skill achievement has no unique saved terminal skill.");
            string key = skill.ID.ToString(CultureInfo.InvariantCulture);
            switch (skill.ID)
            {
                case 1045: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10504, "weapon-skill-fixed-v1", key, 1);
                case 1046: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10505, "weapon-skill-fixed-v1", key, 1);
                case 5030: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10506, "weapon-skill-fixed-v1", key, 1);
                case 5040: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10507, "weapon-skill-fixed-v1", key, 1);
                case 1300: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10508, "weapon-skill-fixed-v1", key, 1);
                case 5020: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10509, "weapon-skill-fixed-v1", key, 1);
                case 1260: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10510, "weapon-skill-fixed-v1", key, 1);
                case 1290: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10511, "weapon-skill-fixed-v1", key, 1);
                case 5010: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10512, "weapon-skill-fixed-v1", key, 1);
                case 1250: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10513, "weapon-skill-fixed-v1", key, 1);
                case 5050: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10514, "weapon-skill-fixed-v1", key, 1);
                case 7000: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10515, "weapon-skill-fixed-v1", key, 1);
                case 7010: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10516, "weapon-skill-fixed-v1", key, 1);
                case 7020: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10517, "weapon-skill-fixed-v1", key, 1);
                case 7030: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10518, "weapon-skill-fixed-v1", key, 1);
                case 7040: return ProgressionEvidence.Complete(c, t, p.Entity.UID, 10519, "weapon-skill-fixed-v1", key, 1);
                default: throw new InvalidOperationException("Unknown weapon skill achievement.");
            }
        }
        // Preserve the existing one-level-per-training-update policy, discarding
        // excess at that threshold, while avoiding multiplication/addition wrap.
        internal static uint Accumulate(uint current, uint incoming, uint threshold, uint rate, uint multiplier,
            uint gems, uint trainingDivisor, uint vipMultiplier)
        {
            if (threshold == 0) return current;
            BigInteger gain = (BigInteger)incoming * Math.Max(1u, rate) * Math.Max(1u, multiplier);
            gain += gain * gems / 100;
            gain /= Math.Max(1u, trainingDivisor);
            gain *= vipMultiplier;
            return (uint)BigInteger.Min(threshold, (BigInteger)current + gain);
        }
    }
}
