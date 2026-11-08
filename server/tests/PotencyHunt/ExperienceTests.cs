using System;
using System.Numerics;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type ExpMath = typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceMath");
    static ulong ExpScale(ulong value, ulong numerator, ulong denominator)
    { return (ulong)Call(ExpMath, null, "Scale", value, numerator, denominator); }
    static void VerifyExperience(Map map)
    {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140");
        var p = Player(map); p.Entity.FullyLoaded = true;
        Check(p.Account.State == AccountTable.AccountState.Player && !p.Fake, "EXP fixture uses an ordinary player");
        var table = (ulong[])typeof(DataHolder).GetField("levelExperience", Hidden).GetValue(null);
        bool exact = true;
        for (int level = 1; level <= byte.MaxValue; level++)
        {
            // Arbitrary-precision oracle checks every input, including table clamping.
            var expected = new BigInteger(table[Math.Min(level - 1, table.Length - 1)]) * 13000
                / (new BigInteger(level) * level * level / 12 + 1);
            typeof(Entity).GetField("_level", Instance).SetValue(p.Entity, (byte)level);
            exact &= p.ExpBall == (ulong)expected;
        }
        Check(exact, "public EXP Ball calculation matches the wide oracle at all 255 nonzero levels");
        Check(DataHolder.LevelExperience(136) == 547859950814UL
            && DataHolder.LevelExperience(137) == 1174030000000UL
            && DataHolder.LevelExperience(138) == 1761040000000UL
            && DataHolder.LevelExperience(139) == 2641550000000UL, "final three stored level thresholds are reachable");
        Check(DataHolder.LevelExperience(0) == 120 && DataHolder.LevelExperience(140) == 2641550000000UL
            && DataHolder.LevelExperience(255) == 2641550000000UL, "level lookup clamps safely at both table boundaries");
        bool rejected = false;
        try { Call(ExpMath, null, "Ball", (byte)0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "zero-level EXP Ball requests are rejected");
        Check((ulong)Call(ExpMath, null, "Ball", (byte)135) == 28396442314UL, "level 135 ball keeps all 28,396,442,314 EXP");
        Check(ExpScale(28396442314UL, 1, 10) == 2839644231UL, "ten-percent EXP reward does not pass through signed 32-bit arithmetic");
        Check(ExpScale(28396442314UL, 299, 300) == 28301787506UL, "fractional offline reward keeps its high bits and floors once");
        Check(ExpScale(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue) == ulong.MaxValue, "overflowing intermediate multiplication retains a representable final award");
        Check(ExpScale(ulong.MaxValue, 1, 10) == 1844674407370955161UL, "maximum wallet fraction is exact without floating-point rounding");
        Check(ExpScale(ulong.MaxValue, 0, 1) == 0 && ExpScale(0, ulong.MaxValue, 1) == 0
            && ExpScale(1, 1, 3) == 0, "zero rewards and sub-unit fractions remain zero");
        rejected = false;
        try { ExpScale(ulong.MaxValue, 2, 1); } catch (OverflowException) { rejected = true; }
        Check(rejected, "unrepresentable final rewards fail instead of wrapping");
        rejected = false;
        try { ExpScale(1, 1, 0); } catch (DivideByZeroException) { rejected = true; }
        Check(rejected, "zero reward denominator is rejected");
        var now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        foreach (var row in new long[][] { new long[] { -1, 0 }, new long[] { 0, 0 }, new long[] { 59, 0 },
            new long[] { 60, 1 }, new long[] { 17999, 299 }, new long[] { 18000, 300 },
            new long[] { 53999, 899 }, new long[] { 54000, 900 }, new long[] { 3932160, 900 } })
            Check((ushort)Call(ExpMath, null, "TrainingMinutes", now.AddSeconds(-row[0]), now) == row[1],
                "offline minutes clamp before narrowing at elapsed seconds " + row[0]);
        Check((ushort)Call(ExpMath, null, "TrainingMinutes", DateTime.MinValue, DateTime.MaxValue) == 900
            && (ushort)Call(ExpMath, null, "TrainingMinutes", DateTime.MaxValue, DateTime.MinValue) == 0,
            "extreme or future training timestamps cannot wrap into a reward");
        // Exercise the production award entry point below the next threshold.
        p.Entity.Level = 135; p.Entity.Experience = 0; p.IncreaseExperience(p.ExpBall, false);
        Check(p.Entity.Level == 135 && p.Entity.Experience == 28396442314UL,
            "ordinary EXP award keeps the full high-level ball in the character cache");
        p.IncreaseExperience(ExpScale(p.ExpBall, 1, 10), false);
        Check(p.Entity.Experience == 31236086545UL, "ordinary fractional reward adds without signed conversion");
        p.Spells[1110] = new Revora.Network.GamePackets.Spell(true) { ID = 1110 };
        foreach (byte level in new byte[] { 135, 136, 137, 138, 139 })
        {
            p.Entity.Level = level; p.Entity.Experience = DataHolder.LevelExperience(level) - 1;
            ulong ball = p.ExpBall; ushort attributes = p.Entity.Atributes;
            p.IncreaseExperience(ball, false);
            Check(p.Entity.Level == level + 1 && p.Entity.Experience == ball - 1
                && p.Entity.Atributes == attributes + Config.AttributePointsPerLevel
                && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == level + 1,
                "production level crossing uses the stored threshold and keeps the wide remainder at level " + level);
        }
        p.Entity.Level = Config.LevelCap; p.Entity.Experience = 123;
        p.IncreaseExperience(p.ExpBall, false);
        Check(p.Entity.Level == Config.LevelCap && p.Entity.Experience == 123,
            "existing maximum-level award policy remains unchanged");
        VerifyExperiencePlan(map);
        VerifyAttributes(map);
        VerifyProgressionStore(map);
        VerifyReincarnationRecovery(map);
        VerifyExperienceEffects(map);
        VerifyMentorExperience(map);
        VerifyDarkHorn(map);
        VerifyWeaponSkillMastery(map);
        VerifyWeaponBooks(map);
        VerifySilverOwnership(map);
        VerifyWeaponShop(map);
    }
}
