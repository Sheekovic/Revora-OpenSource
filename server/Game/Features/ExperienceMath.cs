using System;
using System.Numerics;
using System.Collections.Generic;

namespace Revora.Game.Features
{
    internal static class ExperienceMath
    {
        internal sealed class Progress
        {
            internal readonly byte Level;
            internal readonly ulong Experience;
            internal readonly byte[] Levels;
            internal Progress(byte level, ulong experience, byte[] levels)
            { Level = level; Experience = experience; Levels = levels; }
        }
        // Computes the whole result before any level-up side effect or database write.
        internal static Progress Advance(byte level, ulong experience, ulong award, byte cap,
            byte reborn, byte firstRebornLevel, byte secondRebornLevel)
        {
            if (level == 0 || cap == 0) throw new ArgumentOutOfRangeException("level");
            if (level >= cap) return new Progress(level, experience, new byte[0]);
            var remainder = new BigInteger(experience) + award;
            var levels = new List<byte>();
            byte restored = reborn == 1 ? firstRebornLevel : reborn == 2 ? secondRebornLevel : (byte)0;
            while (level < cap && remainder >= Database.DataHolder.LevelExperience(level))
            {
                remainder -= Database.DataHolder.LevelExperience(level);
                level++;
                if (level >= 130 && restored > 130 && level < restored)
                    level = Math.Min(restored, cap);
                levels.Add(level);
            }
            return new Progress(level, (ulong)remainder, levels.ToArray());
        }
        internal static ulong Ball(byte level)
        {
            if (level == 0) throw new ArgumentOutOfRangeException("level");
            ulong cubic = (ulong)level * level * level;
            return checked(Database.DataHolder.LevelExperience(level) * 13000UL) / (cubic / 12UL + 1UL);
        }
        internal static ulong Scale(ulong value, ulong numerator, ulong denominator)
        {
            if (denominator == 0) throw new DivideByZeroException();
            // The intermediate product can exceed UInt64 even when the final award fits.
            return (ulong)(new BigInteger(value) * numerator / denominator);
        }
        internal static ushort TrainingMinutes(DateTime entered, DateTime now)
        {
            return (ushort)Math.Min(900, Math.Max(0, Math.Floor((now - entered).TotalMinutes)));
        }
    }
}
