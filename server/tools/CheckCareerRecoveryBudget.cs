using System;
using System.Reflection;
using Revora.Database;

// Pure production EXP math. No GameState, persistence, clock override or award.
internal static class CheckCareerRecoveryBudget
{
    static void Require(bool value) { if (!value) throw new InvalidOperationException("Recovery bound failed."); }
    public static int Main(string[] args)
    {
        try
        {
            uint price = uint.Parse(args[0]);
            int cycles = int.Parse(args[1]);
            Require(price > 0 && cycles > 0 && cycles <= 100);
            var type = typeof(DataHolder).Assembly.GetType("Revora.Game.Features.ExperienceMath", true);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var ball = type.GetMethod("Ball", flags);
            var advance = type.GetMethod("Advance", flags);
            int maximum = 0, initial = 0;
            for (byte start = 1; start < 137; start++)
            {
                byte level = start;
                ulong experience = 0;
                int used = 0;
                while (level < 137 && used < 2000)
                {
                    ulong award = (ulong)ball.Invoke(null, new object[] { level });
                    Require(award > 0);
                    var next = advance.Invoke(null, new object[] { level, experience, award, (byte)137, (byte)0, (byte)0, (byte)0 });
                    byte nextLevel = (byte)next.GetType().GetField("Level", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(next);
                    ulong nextExperience = (ulong)next.GetType().GetField("Experience", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(next);
                    Require(nextLevel > level || nextLevel == level && nextExperience > experience);
                    level = nextLevel; experience = nextExperience; used++;
                }
                Require(level == 137 && used < 2000);
                if (start == 1) initial = used;
                if (start >= 15) maximum = Math.Max(maximum, used);
            }
            // Recovery begins at15. Ignore all beneficial saved-level restoration
            // and monster EXP. Reserve another three full initial-life journeys.
            long reserved = checked((long)maximum * cycles + 3L * initial);
            System.Console.WriteLine("{\"start_levels_checked\":136,\"from_level1_balls\":" + initial
                + ",\"maximum_from_level15_or_later_balls\":" + maximum + ",\"recovery_cycles\":" + cycles
                + ",\"additional_initial_lives\":3,\"reserved_balls\":" + reserved
                + ",\"unit_cps\":" + price + ",\"reserved_cps\":" + checked(reserved * price)
                + ",\"target_level\":137,\"claims_level140\":false}");
            return 0;
        }
        catch (Exception error) { System.Console.Error.WriteLine(error); return 1; }
    }
}
