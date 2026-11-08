using System;
using Npgsql;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Immutable run identity for combat editions. Do not change revision one's
    // values when adding another difficulty or balance edition.
    internal static class DisCityRunRules
    {
        internal sealed class Profile
        {
            internal readonly int Difficulty, Revision;
            internal readonly uint OrdinaryHp, GuardianHp, BossHp, MinimumAttack, MaximumAttack, BossMinimumAttack, BossMaximumAttack;
            internal Profile(int difficulty, int revision)
            {
                if (difficulty < 0 || difficulty > 2 || revision != 1) throw new InvalidOperationException("Unknown Dis City combat edition.");
                Difficulty = difficulty; Revision = revision;
                OrdinaryHp = 100000; GuardianHp = 500000; BossHp = 5000000;
                MinimumAttack = 2500; MaximumAttack = 4000; BossMinimumAttack = 5500; BossMaximumAttack = 7500;
                uint scale = (uint)(difficulty + 1);
                OrdinaryHp *= scale; GuardianHp *= scale; BossHp *= scale;
                MinimumAttack = MinimumAttack * (uint)(4 + difficulty) / 4; MaximumAttack = MaximumAttack * (uint)(4 + difficulty) / 4;
                BossMinimumAttack = BossMinimumAttack * (uint)(4 + difficulty) / 4; BossMaximumAttack = BossMaximumAttack * (uint)(4 + difficulty) / 4;
            }
        }
        internal static string Name(int difficulty) { return difficulty == 0 ? "Standard" : difficulty == 1 ? "Veteran" : difficulty == 2 ? "Nightmare" : "Unknown"; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_dis_run_rules(
run uuid PRIMARY KEY,uid bigint NOT NULL,difficulty integer NOT NULL CHECK(difficulty=0),
revision integer NOT NULL CHECK(revision=1));
ALTER TABLE revora_dis_run_rules DROP CONSTRAINT IF EXISTS revora_dis_run_rules_difficulty_check;
ALTER TABLE revora_dis_run_rules ADD CONSTRAINT revora_dis_run_rules_difficulty_check CHECK(difficulty BETWEEN 0 AND 2);
INSERT INTO revora_dis_run_rules(run,uid,difficulty,revision)
SELECT run,uid,0,1 FROM (SELECT run,uid FROM revora_dis_runs UNION SELECT run,uid FROM revora_dis_completions) history
ON CONFLICT(run) DO NOTHING;");
                using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM (SELECT run,uid FROM revora_dis_runs UNION SELECT run,uid FROM revora_dis_completions) r
LEFT JOIN revora_dis_run_rules p ON p.run=r.run WHERE p.run IS NULL OR p.uid<>r.uid OR p.difficulty NOT BETWEEN 0 AND 2 OR p.revision<>1)"))
                    if (Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Dis City existing run rules disagree.");
                t.Commit();
            }}
        }
        internal static void Admit(NpgsqlConnection c, NpgsqlTransaction t, uint uid, Guid run, int difficulty = 0)
        {
            var expected = new Profile(difficulty, 1);
            if (t == null) throw new InvalidOperationException("Dis City rules require the admission transaction.");
            if (Q.Execute(c, t, "INSERT INTO revora_dis_run_rules(run,uid,difficulty,revision) VALUES(@run,@uid,@difficulty,1)",
                "run", run, "uid", (long)uid, "difficulty", difficulty) != 1) throw new InvalidOperationException("Dis City admission rules were not saved.");
            var saved = Read(c, t, uid, run);
            if(saved.Difficulty != expected.Difficulty || saved.Revision != expected.Revision) throw new InvalidOperationException("Dis City admission edition changed.");
        }
        private static Profile Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid, Guid run)
        {
            using (var q = Q.Command(c, t, "SELECT difficulty,revision FROM revora_dis_run_rules WHERE run=@run AND uid=@uid", "run", run, "uid", (long)uid))
            using (var r = q.ExecuteReader()) {
                if (!r.Read()) throw new InvalidOperationException("Dis City run rules are missing or foreign.");
                return new Profile(r.GetInt32(0), r.GetInt32(1));
            }
        }
        internal static Profile Load(uint uid, Guid run)
        {
            using (var c = DataHolder.MySqlConnection) { c.Open(); return Read(c, null, uid, run); }
        }
    }
}
