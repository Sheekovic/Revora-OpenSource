using System;
using System.Collections.Generic;
using Revora.Database;
using Revora.Game.Features.Reincarnation;

namespace Revora.Game.Features
{
    // No packets, character mutation or database writes occur during planning.
    // Reward persistence remains the caller's job.
    internal sealed class ExperienceRewardPlan
    {
        internal sealed class Step
        {
            internal readonly byte Level;
            internal readonly DataHolder.BaseStats Stats;
            internal readonly ushort Attributes;
            // Existing Revora rate; the official guide confirms sources, not this rate.
            internal readonly uint MentorExperience;
            internal Step(byte level, DataHolder.BaseStats stats, ushort attributes, bool restoration = false)
            { Level = level; Stats = stats; Attributes = attributes; MentorExperience = restoration ? 0 : (uint)level * 2; }
        }
        internal readonly CharacterProgression.Profile After;
        internal readonly Step[] Steps;
        internal readonly ushort DefaultSkill;
        internal readonly bool ReachedArenaLevel;
        internal readonly ReincarnateInfo Restored;
        internal readonly bool CreditMentor;

        internal ExperienceRewardPlan(CharacterProgression.Profile before, ulong award, byte cap, ushort pointsPerLevel)
            : this(before, award, cap, pointsPerLevel, null) { }
        internal ExperienceRewardPlan(CharacterProgression.Profile before, ulong award, byte cap, ushort pointsPerLevel, ReincarnateInfo restoration)
            : this(before, award, cap, pointsPerLevel, restoration, true) { }
        internal ExperienceRewardPlan(CharacterProgression.Profile before, ulong award, byte cap, ushort pointsPerLevel, ReincarnateInfo restoration, bool creditMentor)
        {
            if (before == null) throw new ArgumentNullException("before");
            CreditMentor = creditMentor;
            var values = (long[])before.Values.Clone();
            if (restoration != null) ReincarnationRecovery.Validate(restoration);
            bool first = restoration != null && restoration.FirstRebirth;
            bool second = restoration != null && restoration.SecondRebirth;
            bool recordedLife = first || second;
            bool paid = restoration != null && restoration.PaidReincarnation;
            bool pending = restoration != null && !restoration.Recovered && values[8] == (first ? 1 : 2);
            byte threshold = recordedLife ? (byte)130 : (byte)110;
            // A recorded first or second life, including an already consumed one, uses only
            // its atomic recovery. Never also apply the old level-only shortcut.
            byte firstLevel = first || paid ? (byte)0 : (byte)values[11];
            byte secondLevel = second || paid ? (byte)0 : (byte)values[12];
            byte start = (byte)values[0];
            bool canRestore = restoration != null && (recordedLife || paid ? start <= restoration.Level && start <= cap : start < restoration.Level && start < cap);
            var steps = new List<Step>();
            // Restore before spending the award on the former lower-level curve.
            // Reincarnation records at/above the former level are stale. An
            // unconsumed life receipt at that exact level can still owe EXP.
            var progress = pending && start >= threshold && canRestore
                ? new ExperienceMath.Progress(start, checked((ulong)values[1] + award), new byte[0])
                : ExperienceMath.Advance(start, (ulong)values[1], award,
                    pending && start < threshold ? Math.Min(cap, threshold) : cap,
                    (byte)values[8], firstLevel, secondLevel);
            AddSteps(values, steps, progress.Levels, pointsPerLevel);
            if (pending && progress.Level >= threshold)
            {
                Restored = new ReincarnateInfo { UID = restoration.UID, Level = restoration.Level, Experience = restoration.Experience, FirstRebirth = first, SecondRebirth = second, RebirthClass = restoration.RebirthClass, PaidReincarnation = paid, PaidReceipt = restoration.PaidReceipt };
                byte target = Math.Max(progress.Level, Math.Min(restoration.Level, cap));
                if (canRestore)
                {
                    int recoveredLevels = target - progress.Level;
                    values[6] = checked((ushort)(values[6] + recoveredLevels * pointsPerLevel));
                    if (recoveredLevels != 0) steps.Add(new Step(target, null, (ushort)values[6], true));
                    progress = target >= cap
                        ? new ExperienceMath.Progress(target, checked(restoration.Experience + progress.Experience), new byte[0])
                        : ExperienceMath.Advance(target, restoration.Experience, progress.Experience, cap,
                            (byte)values[8], firstLevel, secondLevel);
                    AddSteps(values, steps, progress.Levels, pointsPerLevel);
                }
            }
            values[0] = progress.Level;
            values[1] = checked((long)progress.Experience);
            Steps = steps.ToArray(); After = new CharacterProgression.Profile(values);
            foreach (var step in Steps) if (step.Level == 70) ReachedArenaLevel = true;
            if (Steps.Length != 0)
            {
                byte job = (byte)values[7];
                if (job >= 10 && job <= 15) DefaultSkill = 1110;
                else if (job >= 20 && job <= 25) DefaultSkill = 1020;
                else if (job >= 40 && job <= 45) DefaultSkill = 8002;
                else if (job >= 50 && job <= 55) DefaultSkill = 6011;
                else if (job >= 60 && job <= 65) DefaultSkill = 10490;
            }
        }
        private static void AddSteps(long[] values, List<Step> steps, byte[] levels, ushort pointsPerLevel)
        {
            byte previousLevel = (byte)values[0];
            foreach (byte level in levels)
            {
                DataHolder.BaseStats stats = null;
                if (values[8] == 0 && level <= 120)
                {
                    stats = DataHolder.StatsFor((byte)values[7], level);
                    CharacterProgression.AssignBaseStats(values, stats, (byte)values[7], previousLevel);
                }
                else values[6] = checked((ushort)(values[6] + pointsPerLevel));
                steps.Add(new Step(level, stats, (ushort)values[6]));
                previousLevel = level;
            }
        }
    }
}
