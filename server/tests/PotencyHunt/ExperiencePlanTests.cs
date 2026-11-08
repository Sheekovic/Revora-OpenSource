using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static object ExpPlan(byte level, ulong experience, ulong award, byte cap = 140,
        byte reborn = 0, byte first = 0, byte second = 0)
    { return Call(ExpMath, null, "Advance", level, experience, award, cap, reborn, first, second); }
    static bool ExpResult(object result, byte level, ulong experience, params byte[] steps)
    {
        var type = result.GetType();
        return (byte)type.GetField("Level", Instance).GetValue(result) == level
            && (ulong)type.GetField("Experience", Instance).GetValue(result) == experience
            && ((byte[])type.GetField("Levels", Instance).GetValue(result)).SequenceEqual(steps);
    }
    static void VerifyExperiencePlan(Map map)
    {
        ulong requirement = DataHolder.LevelExperience(135);
        Check(ExpResult(ExpPlan(135, requirement - 1, 0), 135, requirement - 1), "planned award below threshold preserves its level and exact remainder");
        Check(ExpResult(ExpPlan(135, requirement - 1, 1), 136, 0, 136), "planned exact threshold subtracts once");
        ulong combined = requirement + DataHolder.LevelExperience(136) + DataHolder.LevelExperience(137);
        Check(ExpResult(ExpPlan(135, 0, combined + 19), 138, 19, 136, 137, 138), "planned multi-level award records every ordinary level-up effect in order");
        Check(ExpResult(ExpPlan(129, DataHolder.LevelExperience(129) - 1, 1, 140, 1, 137, 139), 137, 0, 137), "first rebirth uses the first stored level at the 130 threshold");
        Check(ExpResult(ExpPlan(129, DataHolder.LevelExperience(129) - 1, 1, 140, 2, 137, 139), 139, 0, 139), "second rebirth uses the second stored level at the 130 threshold");
        Check(ExpResult(ExpPlan(129, DataHolder.LevelExperience(129) - 1, 1, 135, 1, 139, 0), 135, 0, 135), "rebirth restoration respects a lower configured cap");
        Check(ExpResult(ExpPlan(129, 0, 1, 140, 1, 139, 0), 129, 1), "rebirth restoration cannot happen before a level threshold is earned");
        Check(ExpResult(ExpPlan(139, 123, ulong.MaxValue), 140, ulong.MaxValue - DataHolder.LevelExperience(139) + 123, 140),
            "wide intermediate addition can cross the cap and retain a representable final remainder");
        Check(ExpResult(ExpPlan(140, 123, ulong.MaxValue), 140, 123)
            && ExpResult(ExpPlan(140, 123, ulong.MaxValue, 135), 140, 123), "characters already at or above the cap are unchanged");
        Check(ExpResult(ExpPlan(254, DataHolder.LevelExperience(254), 0, 255), 255, 0, 255), "maximum byte cap cannot wrap the planned level to zero");
        bool rejected = false;
        try { ExpPlan(139, ulong.MaxValue, ulong.MaxValue); } catch (OverflowException) { rejected = true; }
        Check(rejected, "unrepresentable final EXP fails during planning before any side effects");
        rejected = false; try { ExpPlan(0, 0, 1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "zero-level plan is invalid");
        rejected = false; try { ExpPlan(1, 0, 1, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "zero-cap plan is invalid");
        var p = Player(map); p.Entity.FullyLoaded = true; p.Entity.Level = 135;
        p.Entity.Experience = requirement - 1; p.Entity.Atributes = ushort.MaxValue;
        p.Spells[1110] = new Revora.Network.GamePackets.Spell(true) { ID = 1110 };
        rejected = false; try { p.IncreaseExperience(1, false); } catch (OverflowException) { rejected = true; }
        Check(rejected && p.Entity.Level == 135 && p.Entity.Experience == requirement - 1
            && p.Entity.Atributes == ushort.MaxValue && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 135,
            "attribute-cap failure leaves level, EXP, points and durable level unchanged");
        p.Entity.Atributes = 0; p.Entity.Experience = 0;
        Parallel.For(0, 2000, i => p.IncreaseExperience(1, false));
        Check(p.Entity.Experience == 2000, "concurrent ordinary EXP increments cannot overwrite one another");
        typeof(Entity).GetField("PersistenceRequiresReload", Instance).SetValue(p.Entity, true);
        p.IncreaseExperience(1, false); DemonReset(p);
        Check(p.Entity.Experience == 2000, "fenced characters cannot accrue more unsavable EXP");
        p.Entity.InAutoHunt = true; p.Entity.AutoHuntEXP = ulong.MaxValue;
        rejected = false;
        try { p.IncreaseExperience(Math.Max(1u, Config.AutoHuntExperienceDivisor), false); }
        catch (OverflowException) { rejected = true; }
        p.Entity.InAutoHunt = false;
        Check(rejected && p.Entity.AutoHuntEXP == ulong.MaxValue && p.Entity.Experience == 2000
            && p.Entity.Level == 135 && p.Entity.Atributes == 0,
            "auto-hunt credit overflow is rejected before changing character progression");
        DataHolder.ReadStats(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        var statsFile = new IniFile("database/Stats.ini");
        byte[] classes = { 15, 25, 45, 55, 65, 75, 135, 145, 165 };
        string[] profiles = { "Trojan", "Warrior", "Archer", "Ninja", "Monk", "Monk", "Taoist", "Taoist", "WindWalker" };
        ushort[] skills = { 1110, 1020, 8002, 6011, 10490, 0, 0, 0, 0 };
        for (int index = 0; index < classes.Length; index++)
        {
            var player = RunePlayer(map); player.Entity.FullyLoaded = false;
            player.Entity.Class = classes[index]; player.Entity.Level = 119; player.Entity.Atributes = 10;
            player.Entity.Experience = DataHolder.LevelExperience(119) - 1; player.Entity.FullyLoaded = true;
            player.IncreaseExperience(1, false);
            var stats = statsFile.ReadString("Stats", profiles[index] + "[120]").Split(',').Select(ushort.Parse).ToArray();
            Check(player.Entity.Level == 120 && player.Entity.Experience == 0 && player.Entity.Atributes == 10
                && player.Entity.Strength == stats[0] && player.Entity.Vitality == stats[1]
                && player.Entity.Agility == stats[2] && player.Entity.Spirit == stats[3]
                && (skills[index] == 0 || player.Spells.ContainsKey(skills[index])),
                "ordinary level 120 keeps installed base stats and existing class-skill behavior for class " + classes[index]);
            player.IncreaseExperience(DataHolder.LevelExperience(120), false);
            Check(player.Entity.Level == 121 && player.Entity.Experience == 0
                && player.Entity.Atributes == 10 + Config.AttributePointsPerLevel
                && player.Entity.Strength == stats[0] && player.Entity.Vitality == stats[1],
                "level 121 changes to free-point rewards without replacing base stats for class " + classes[index]);
        }
        foreach (byte reborn in new byte[] { 1, 2 })
        {
            var player = RunePlayer(map); player.Entity.FullyLoaded = false;
            player.Entity.Level = 129; player.Entity.Reborn = reborn;
            player.Entity.FirstRebornLevel = 137; player.Entity.SecondRebornLevel = 139;
            player.Entity.Atributes = 10; player.Entity.Experience = DataHolder.LevelExperience(129) - 1;
            player.Entity.FullyLoaded = true; player.IncreaseExperience(1, false);
            Check(player.Entity.Level == (reborn == 1 ? 137 : 139) && player.Entity.Experience == 0
                && player.Entity.Atributes == 10 + Config.AttributePointsPerLevel,
                "production award preserves existing rebirth restoration and per-transition point policy for rebirth " + reborn);
        }
        VerifyRewardPlan(map);
    }

    static object RewardPlan(GameState p, ulong award, byte cap = 140)
    {
        lock (p.ItemSyncRoot)
        {
            var before = Call(ProgressionStore, null, "Capture", p);
            var type = typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceRewardPlan");
            try { return Activator.CreateInstance(type, Instance, null,
                new object[] { before, award, cap, Config.AttributePointsPerLevel }, null); }
            catch (TargetInvocationException ex)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
    static long[] RewardProfile(object plan)
    {
        object after = plan.GetType().GetField("After", Instance).GetValue(plan);
        return (long[])after.GetType().GetField("Values", Instance).GetValue(after);
    }
    static void VerifyRewardPlan(Map map)
    {
        var p = RunePlayer(map); p.Entity.FullyLoaded = false; p.Entity.Level = 119;
        p.Entity.Atributes = 10; p.Entity.Experience = 7; p.Entity.Strength = 1;
        p.Entity.Vitality = 2; p.Entity.Agility = 3; p.Entity.Spirit = 4; p.Entity.FullyLoaded = true;
        ulong award = DataHolder.LevelExperience(119) + DataHolder.LevelExperience(120) + DataHolder.LevelExperience(121);
        object plan = RewardPlan(p, award); var values = RewardProfile(plan);
        var expected = new IniFile("database/Stats.ini").ReadString("Stats", "Trojan[120]").Split(',').Select(ushort.Parse).ToArray();
        Check(values[0] == 122 && values[1] == 7 && values[6] == 10 + Config.AttributePointsPerLevel * 2
            && values[2] == expected[0] && values[3] == expected[2] && values[4] == expected[1] && values[5] == expected[3],
            "complete reward plan crosses automatic stats into free points and preserves the exact remainder");
        Check(p.Entity.Level == 119 && p.Entity.Experience == 7 && p.Entity.Atributes == 10
            && p.Entity.Strength == 1 && p.Entity.Vitality == 2 && p.Entity.Agility == 3 && p.Entity.Spirit == 4
            && !p.Spells.ContainsKey(1110), "planning does not mutate the character or grant its class skill");
        p.IncreaseExperience(award, false);
        object actual; lock (p.ItemSyncRoot) { actual = Call(ProgressionStore, null, "Capture", p); }
        Check(values.SequenceEqual((long[])actual.GetType().GetField("Values", Instance).GetValue(actual)),
            "ordinary production award applies the full precomputed thirteen-field profile");
        p.Entity.Level = 69; p.Entity.Experience = 0;
        plan = RewardPlan(p, DataHolder.LevelExperience(69) + DataHolder.LevelExperience(70));
        Check((bool)plan.GetType().GetField("ReachedArenaLevel", Instance).GetValue(plan)
            && RewardProfile(plan)[0] == 71 && (ushort)plan.GetType().GetField("DefaultSkill", Instance).GetValue(plan) == 1110,
            "multi-level planning records arena admission and class skill side effects before application");
        p.Entity.Level = 140; p.Entity.Experience = 17;
        plan = RewardPlan(p, ulong.MaxValue);
        Check(RewardProfile(plan)[0] == 140 && RewardProfile(plan)[1] == 17
            && ((Array)plan.GetType().GetField("Steps", Instance).GetValue(plan)).Length == 0
            && (ushort)plan.GetType().GetField("DefaultSkill", Instance).GetValue(plan) == 0
            && !(bool)plan.GetType().GetField("ReachedArenaLevel", Instance).GetValue(plan),
            "capped rewards carry no level-up, skill or arena effects");
        p.Entity.Level = 139; p.Entity.Experience = 0;
        bool rejected = false;
        try { p.IncreaseExperience(ulong.MaxValue, false); } catch (OverflowException) { rejected = true; }
        Check(rejected && p.Entity.Level == 139 && p.Entity.Experience == 0,
            "ordinary rewards reject an EXP result above database bigint capacity before mutation");

        var bad = RunePlayer(map); bad.Entity.FullyLoaded = false; bad.Entity.Level = 118;
        bad.Entity.Experience = 0; bad.Entity.Strength = 1; bad.Entity.Vitality = 2;
        bad.Entity.Agility = 3; bad.Entity.Spirit = 4; bad.Entity.FullyLoaded = true;
        var profiles = (SafeDictionary<byte, string>)typeof(DataHolder).GetField("TrojanStats", Hidden).GetValue(null);
        string saved = profiles[120];
        try
        {
            profiles[120] = "100,200,300,invalid";
            rejected = false;
            try { DataHolder.GetStats(15, 120, bad); } catch (FormatException) { rejected = true; }
            Check(rejected && bad.Entity.Strength == 1 && bad.Entity.Vitality == 2 && bad.Entity.Agility == 3 && bad.Entity.Spirit == 4,
                "malformed last stat field cannot partially apply the earlier three fields");
            rejected = false;
            try { bad.IncreaseExperience(DataHolder.LevelExperience(118) + DataHolder.LevelExperience(119), false); }
            catch (FormatException) { rejected = true; }
            Check(rejected && bad.Entity.Level == 118 && bad.Entity.Experience == 0 && bad.Entity.Strength == 1
                && bad.Entity.Vitality == 2 && bad.Entity.Agility == 3 && bad.Entity.Spirit == 4
                && !bad.Spells.ContainsKey(1110), "invalid later stat profile rejects the whole award before earlier level and skill effects");
            profiles[120] = "1,2,3,4,5"; rejected = false;
            try { RewardPlan(bad, DataHolder.LevelExperience(118) + DataHolder.LevelExperience(119)); }
            catch (FormatException) { rejected = true; }
            Check(rejected, "unexpected extra stat fields are rejected instead of silently ignored");
        }
        finally { profiles[120] = saved; }
        VerifyLevelMentorSources(map);
    }
    static void VerifyLevelMentorSources(Map map)
    {
        Query(@"CREATE TABLE apprentice(apprenticeid bigint PRIMARY KEY, mentorid bigint, actual_experience bigint,
total_experience bigint, actual_plus integer, total_plus integer, actual_heavenblessing integer, total_heavenblessing integer)");
        var mentor = RunePlayer(map); var student = RunePlayer(map);
        mentor.Entity.FullyLoaded = true;
        var app = new Revora.Game.ConquerStructures.Society.Apprentice { ID = student.Entity.UID,
            Actual_Experience = 100, Total_Experience = 1000, Actual_Plus = 20, Total_Plus = 120,
            Actual_HeavenBlessing = 30, Total_HeavenBlessing = 130 };
        mentor.Apprentices = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Apprentice>();
        mentor.Apprentices.Add(student.Entity.UID, app);
        mentor.PrizeExperience = 100; mentor.PrizePlusStone = 20; mentor.PrizeHeavenBlessing = 30;
        student.Mentor = new Revora.Game.ConquerStructures.Society.Mentor { ID = mentor.Entity.UID };
        Query(@"INSERT INTO apprentice VALUES(@uid,@mentor,100,1000,20,120,30,130)",
            "uid", (long)student.Entity.UID, "mentor", (long)mentor.Entity.UID);
        student.Entity.FullyLoaded = false; student.Entity.Level = 135;
        student.Entity.Experience = 0; student.Entity.FullyLoaded = true;
        student.IncreaseExperience(DataHolder.LevelExperience(135) + DataHolder.LevelExperience(136), false);
        Check(student.Entity.Level == 137 && mentor.PrizeExperience == 646 && app.Actual_Experience == 646
            && app.Total_Experience == 1546, "multi-level award preserves Revora's existing mentor EXP contribution rate");
        Check(mentor.PrizePlusStone == 20 && mentor.PrizeHeavenBlessing == 30 && app.Actual_Plus == 20
            && app.Total_Plus == 120 && app.Actual_HeavenBlessing == 30 && app.Total_HeavenBlessing == 130,
            "level-ups cannot mint mentor +1 stone credit or blessing time");
        Check(Scalar(@"SELECT count(*) FROM apprentice WHERE apprenticeid=@uid AND actual_experience=646
AND total_experience=1546 AND actual_plus=20 AND total_plus=120 AND actual_heavenblessing=30 AND total_heavenblessing=130",
            student.Entity.UID) == 1, "production apprentice save retains EXP credit without persisting unrelated level-up rewards");
    }
}
