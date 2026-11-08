using System;

namespace Revora.Game.Features
{
    // Pure planning only. A caller must prove canonical combat, owned material
    // payment and durable stage/encounter preimages in its transaction.
    // Evidence and unresolved world integration: docs/ABYSS_TRIAL_RESTORATION.md.
    internal static class AbyssTrialRules
    {
        internal const ushort Map = 1700;
        internal const int RequiredVigor = 3, RequiredGrievance = 40000, RequiredSpirits = 72;
        internal enum Stage : byte { Vigor = 1, Grievance = 2, Spirits = 3, Satan = 4, Complete = 5 }
        internal sealed class Progress {
            internal readonly Stage Step;
            internal readonly int Meters, Spirits, SatanForms;
            internal Progress(Stage step, int meters = 0, int spirits = 0, int satanForms = 0) {
                if (step < Stage.Vigor || step > Stage.Complete || meters < 0 || meters > RequiredGrievance
                    || spirits < 0 || spirits > RequiredSpirits || satanForms < 0 || satanForms > 3
                    || step == Stage.Vigor && (meters != 0 || spirits != 0 || satanForms != 0)
                    || step == Stage.Grievance && (spirits != 0 || satanForms != 0)
                    || step >= Stage.Spirits && meters != RequiredGrievance
                    || step == Stage.Spirits && satanForms != 0
                    || step >= Stage.Satan && spirits != RequiredSpirits
                    || step == Stage.Satan && satanForms >= 3 || step == Stage.Complete && satanForms != 3)
                    throw new ArgumentException("Invalid Abyss trial progress.");
                Step = step; Meters = meters; Spirits = spirits; SatanForms = satanForms;
            }
        }
        internal struct Material {
            internal readonly uint Type; internal readonly ushort Count;
            internal Material(uint type, ushort count) { Type = type; Count = count; }
        }
        internal static bool Eligible(byte reborn, byte level) { return reborn == 1 && level >= 120; }
        internal static Material[] ExchangeCost(uint output) {
            switch (output) {
                case 722721: return new[] { new Material(722722, 3) };
                case 722728: return new[] { new Material(722726, 3) };
                case 722730: return new[] { new Material(722729, 3) };
                default: throw new ArgumentException("Unknown Abyss exchange.");
            }
        }
        internal static Material[] SummonCost(uint family) {
            var moss = new Material(722723, 1); var grass = new Material(722724, 1); var aroma = new Material(722725, 1);
            switch (family) {
                case 3632: return new[] { moss, grass, aroma };
                case 3633: return new[] { moss, grass, aroma, new Material(722721, 1) };
                case 3634: return new[] { moss, grass, aroma, new Material(722728, 1) };
                case 3635: return new[] { new Material(722730, 1) };
                case 3644: return new[] { new Material(722727, 1) };
                default: throw new ArgumentException("Unknown Abyss summon.");
            }
        }
        internal static uint SummonReward(uint family) {
            switch (family) { case 3632: return 722722; case 3633: return 722726; case 3634: return 722729; case 3635: return 722731;
                default: throw new ArgumentException("This encounter has no material reward."); }
        }
        internal static int GrievanceWeight(uint family) {
            if (family < 3600 || family > 3631) return 0;
            switch ((family - 3600) % 4) { case 0: return 1; case 1: return 6; case 2: return 20; default: return 500; }
        }
        internal static bool BeadSource(uint family) { return family == 3603 || family == 3607 || family == 3611 || family == 3619; }
        internal static uint NextSpirit(Progress p) { return p.Step == Stage.Spirits && p.Spirits < RequiredSpirits ? 3636u + (uint)(p.Spirits % 8) : 0; }
        internal static Progress TurnIn(Progress p, uint npc, int offeredVigor = 0) {
            if (p.Step == Stage.Vigor && npc == 3601 && offeredVigor == RequiredVigor) return new Progress(Stage.Grievance);
            if (p.Step == Stage.Grievance && npc == 3602 && p.Meters == RequiredGrievance) return new Progress(Stage.Spirits, p.Meters);
            if (p.Step == Stage.Spirits && npc == 3603 && p.Spirits == RequiredSpirits) return new Progress(Stage.Satan, p.Meters, p.Spirits);
            return p;
        }
        internal static Progress Kill(Progress p, uint family, bool ownedEncounter) {
            if (p.Step == Stage.Grievance) return new Progress(p.Step, Math.Min(RequiredGrievance, p.Meters + GrievanceWeight(family)));
            if (p.Step == Stage.Spirits && ownedEncounter && NextSpirit(p) == family && family != 0) return new Progress(p.Step, p.Meters, p.Spirits + 1);
            // 3646 is the logical third phase. The world supplies its explicit
            // Revora profile instead of relying on the missing catalog family.
            if (p.Step == Stage.Satan && ownedEncounter && family == 3644u + p.SatanForms)
                return new Progress(p.SatanForms == 2 ? Stage.Complete : Stage.Satan, p.Meters, p.Spirits, p.SatanForms + 1);
            return p;
        }
    }
}
