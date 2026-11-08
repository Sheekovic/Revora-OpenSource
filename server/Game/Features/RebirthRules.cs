using System;
using System.Linq;
using Revora.Database;

namespace Revora.Game.Features
{
    // Patch-6711 first-life rules. See docs/FIRST_REBIRTH_COMPLETION.md for evidence.
    internal static class RebirthRules
    {
        internal static readonly byte[] Classes = { 11, 21, 41, 51, 61, 71, 81, 132, 142, 161 };
        internal static readonly string[] Names = { "Trojan", "Warrior", "Archer", "Ninja", "Monk", "Pirate", "Dragon Warrior", "Water Taoist", "Fire Taoist", "Windwalker" };
        internal static bool Eligible(byte profession, byte level, byte reborn) {
            return reborn == 0 && Classes.Any(c => c / 10 == profession / 10) && profession % 10 == 5
                && level >= (profession == 135 ? 110 : 120);
        }
        internal static ushort SelectedBody(ushort current, bool sturdy) {
            // Character creation admits 1006/2005; the rite offers the existing two sizes.
            switch (current) {
                case 1003: case 1004: case 1006: return sturdy ? (ushort)1004 : (ushort)1003;
                case 2001: case 2002: case 2005: return sturdy ? (ushort)2002 : (ushort)2001;
                default: return 0;
            }
        }
        internal static byte Bonus(byte level, byte profession) {
            int steps = profession == 135 ? (Math.Max(110, (int)level) - 110) / 2 : Math.Max(120, (int)level) - 120;
            steps = Math.Min(10, steps);
            return (byte)(steps * (steps + 1) / 2);
        }
        internal static CharacterProgression.Profile Profile(byte oldClass, byte oldLevel, byte nextClass) {
            if (!Eligible(oldClass, oldLevel, 0) || !Classes.Contains(nextClass)) throw new ArgumentException("Invalid first rebirth class or level.");
            // Ten base points, 42 level points, and twenty freely allotted rebirth points.
            // Use the installed level-15 distribution; keep the complete total invariant.
            var s = DataHolder.StatsFor(nextClass, 15);
            int total = 72 + Bonus(oldLevel, oldClass), assigned = s.Strength + s.Agility + s.Vitality + s.Spirit;
            if (assigned > total) throw new InvalidOperationException("Rebirth stat profile exceeds its attribute budget.");
            return new CharacterProgression.Profile(15, 0, s.Strength, s.Agility, s.Vitality, s.Spirit,
                total - assigned, nextClass, 1, oldClass, 0, oldLevel, 0);
        }
    }
}
