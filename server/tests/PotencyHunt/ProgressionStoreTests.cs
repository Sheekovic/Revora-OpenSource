using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Npgsql;

internal static partial class PotencyHuntTests
{
    static readonly Type ProgressionStore = typeof(Entity).Assembly.GetType("Revora.Game.Features.CharacterProgression");
    static void LoadProgressionImage(GameState p)
    {
        using (var query = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
        using (var reader = new MySqlReader(query))
        {
            if (!reader.Read()) throw new Exception("Fixture character missing.");
            Call(ProgressionStore, null, "Loaded", p, reader);
        }
    }
    static GameState StoredProgressionPlayer(Map map)
    {
        var p = RunePlayer(map); p.Entity.DailySignDays = new byte[0]; p.Entity.FullyLoaded = false;
        p.Entity.Level = 135; p.Entity.Strength = 100; p.Entity.Vitality = 100; p.Entity.Atributes = 30;
        p.Entity.Experience = 5000; p.Entity.FullyLoaded = true;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Initial fixture save failed.");
        LoadProgressionImage(p); return p;
    }
    static object NextProgression(GameState p, byte level, ulong experience, ushort attributes)
    {
        lock (p.ItemSyncRoot) {
        var profile = Call(ProgressionStore, null, "Capture", p);
        var values = (long[])profile.GetType().GetField("Values", Instance).GetValue(profile);
        values[0] = level; values[1] = checked((long)experience); values[6] = attributes;
        return Activator.CreateInstance(profile.GetType(), Instance, null, new object[] { values }, null);
        }
    }
    static void VerifyProgressionStore(Map map)
    {
        var p = StoredProgressionPlayer(map);
        Check((bool)Call(ProgressionStore, null, "Managed", p), "production reader registers an ordinary character's durable progression image");
        var nullable = StoredProgressionPlayer(map);
        Query("UPDATE entities SET firstrebornclass=NULL,secondrebornclass=NULL,firstrebornlevel=NULL,secondrebornlevel=NULL WHERE uid=@uid", "uid", (long)nullable.Entity.UID);
        LoadProgressionImage(nullable);
        Check(EntityTable.SaveEntity(nullable) && !Fenced(nullable)
            && Scalar("SELECT count(*) FROM entities WHERE uid=@uid AND firstrebornclass=0 AND secondrebornlevel=0", nullable.Entity.UID) == 1,
            "conditional save preserves null preimages while writing the loader's zero defaults");
        Query("UPDATE entities SET firstrebornclass=NULL WHERE uid=@uid", "uid", (long)nullable.Entity.UID);
        LoadProgressionImage(nullable); nullable.Entity.Level = 136;
        Check(!Fenced(nullable) && EntityTable.SaveEntity(nullable), "immediate profile writes also match nullable legacy fields safely");
        var invalid = StoredProgressionPlayer(map);
        Query("UPDATE entities SET experience=-1 WHERE uid=@uid", "uid", (long)invalid.Entity.UID);
        bool invalidRejected = false;
        try { LoadProgressionImage(invalid); } catch (OverflowException) { invalidRejected = true; }
        Check(invalidRejected && Fenced(invalid) && Scalar("SELECT experience FROM entities WHERE uid=@uid", invalid.Entity.UID) == -1,
            "strict progression loading rejects negative stored EXP rather than treating it as zero");
        p.IncreaseExperience(100, false);
        Revora.Network.PacketHandler.HandlePacket(AttributePacket(10), p);
        Check(Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == 5000
            && p.Entity.Experience == 5100 && p.Entity.Strength == 110 && p.Entity.Atributes == 20,
            "pending EXP and allocated points remain distinct from the last durable image");
        Check(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(p)
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == 5100,
            "conditional production saves persist pending progression and advance the expected image");
        p.IncreaseExperience(25, false); p.Entity.Level = 136;
        Check(!Fenced(p) && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 136
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == 5100
            && p.Entity.Experience == 5125 && EntityTable.SaveEntity(p),
            "immediate level writes track their saved result without discarding pending EXP");
        foreach (var row in new object[][] { new object[] { "Class", (byte)25 }, new object[] { "Reborn", (byte)1 },
            new object[] { "FirstRebornClass", (byte)15 }, new object[] { "SecondRebornClass", (byte)25 } })
        {
            typeof(Entity).GetProperty((string)row[0]).SetValue(p.Entity, row[1], null);
            Check(!Fenced(p) && EntityTable.SaveEntity(p), "immediate " + row[0] + " changes stay coordinated with the full save");
        }
        var stale = StoredProgressionPlayer(map);
        Query("UPDATE entities SET experience=6000 WHERE uid=@uid", "uid", (long)stale.Entity.UID);
        stale.IncreaseExperience(1, false);
        Check(!EntityTable.SaveEntity(stale) && Fenced(stale)
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", stale.Entity.UID) == 6000,
            "stale full save fences and preserves an external EXP change");
        var changedStat = StoredProgressionPlayer(map);
        Query("UPDATE entities SET strength=200 WHERE uid=@uid", "uid", (long)changedStat.Entity.UID);
        bool rejected = false;
        try { changedStat.Entity.Level = 136; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Fenced(changedStat) && Scalar("SELECT strength FROM entities WHERE uid=@uid", changedStat.Entity.UID) == 200
            && Scalar("SELECT level FROM entities WHERE uid=@uid", changedStat.Entity.UID) == 135,
            "stale immediate level write cannot overwrite another saved stat field");
        foreach (string field in new[] { "level", "experience", "strength", "agility", "vitality", "spirit", "atributes",
            "class", "reborn", "firstrebornclass", "secondrebornclass", "firstrebornlevel", "secondrebornlevel" })
        {
            var changed = StoredProgressionPlayer(map);
            long expected = Scalar("SELECT " + field + " FROM entities WHERE uid=@uid", changed.Entity.UID) + 1;
            Query("UPDATE entities SET " + field + "=" + field + "+1 WHERE uid=@uid", "uid", (long)changed.Entity.UID);
            Check(!EntityTable.SaveEntity(changed) && Fenced(changed)
                && Scalar("SELECT " + field + " FROM entities WHERE uid=@uid", changed.Entity.UID) == expected,
                "conditional save preserves an external change to " + field);
        }
        var transactional = StoredProgressionPlayer(map); var item = Pack(transactional, 1088001, 1);
        var next = NextProgression(transactional, 136, 12345, 33);
        Query("CREATE TABLE progression_failure_fixture(uid bigint, receipt integer CHECK(receipt>0))");
        bool rolledBack = false;
        try { lock (transactional.ItemSyncRoot)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    Call(ProgressionStore, null, "Persist", c, t, transactional, next);
                    using (var q = new NpgsqlCommand("DELETE FROM items WHERE uid=@item AND entityid=@owner", c, t))
                    { q.Parameters.AddWithValue("item", (long)item.UID); q.Parameters.AddWithValue("owner", (long)transactional.Entity.UID); q.ExecuteNonQuery(); }
                    using (var q = new NpgsqlCommand("INSERT INTO progression_failure_fixture(uid,receipt) VALUES(@uid,0)", c, t))
                    { q.Parameters.AddWithValue("uid", (long)transactional.Entity.UID); q.ExecuteNonQuery(); }
                    t.Commit();
                }
            }
        }} catch (PostgresException) { rolledBack = true; }
        Check(rolledBack && Scalar("SELECT experience FROM entities WHERE uid=@uid", transactional.Entity.UID) == 5000
            && SavedRune(item, "entityid") == transactional.Entity.UID && transactional.Entity.Experience == 5000
            && EntityTable.SaveEntity(transactional), "failed receipt insertion rolls back progression and item consumption without advancing the loaded image");
        lock (transactional.ItemSyncRoot)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                { Call(ProgressionStore, null, "Persist", c, t, transactional, next); t.Commit(); }
            }
            // A trigger rejecting any further UPDATE proves projection emits no second write.
            Query(@"CREATE FUNCTION reject_progression_projection() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'projection must not write'; END $$;
CREATE TRIGGER reject_progression_projection BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_progression_projection();");
            try { Call(ProgressionStore, null, "ApplyCommitted", transactional, next); }
            finally { Query("DROP TRIGGER reject_progression_projection ON entities; DROP FUNCTION reject_progression_projection()"); }
        }
        Check(!Fenced(transactional) && transactional.Entity.Level == 136 && transactional.Entity.Experience == 12345
            && transactional.Entity.Atributes == 33 && EntityTable.SaveEntity(transactional),
            "committed profile projects without a second database mutation and survives the next production save");
        var racer = StoredProgressionPlayer(map); bool[] saves = new bool[20];
        Parallel.Invoke(() => { for (int i = 0; i < 100; i++) racer.IncreaseExperience(1, false); },
            () => { for (int i = 0; i < saves.Length; i++) saves[i] = EntityTable.SaveEntity(racer); });
        Check(saves.All(value => value) && EntityTable.SaveEntity(racer) && !Fenced(racer)
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", racer.Entity.UID) == 5100,
            "conditional saves and concurrent ordinary awards preserve every EXP gain");
        racer.Entity.Experience = ulong.MaxValue;
        Check(!EntityTable.SaveEntity(racer) && Fenced(racer)
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", racer.Entity.UID) == 5100,
            "EXP outside the production signed-bigint range fails without changing durable progression");
        VerifyPlannedProfileTransaction(map);
    }
    static void VerifyPlannedProfileTransaction(Map map)
    {
        var p = StoredProgressionPlayer(map); p.Entity.Level = 119; p.Entity.Experience = 0;
        Check(EntityTable.SaveEntity(p), "planned transaction starts from an ordinary saved level-119 character");
        var plan = RewardPlan(p, DataHolder.LevelExperience(119) + DataHolder.LevelExperience(120));
        var after = plan.GetType().GetField("After", Instance).GetValue(plan);
        var expected = RewardProfile(plan);
        bool rejected = false;
        lock (p.ItemSyncRoot)
        {
            try
            {
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        Call(ProgressionStore, null, "Persist", c, t, p, after);
                        using (var q = new NpgsqlCommand("INSERT INTO progression_failure_fixture(uid,receipt) VALUES(@uid,0)", c, t))
                        { q.Parameters.AddWithValue("uid", (long)p.Entity.UID); q.ExecuteNonQuery(); }
                        t.Commit();
                    }
                }
            }
            catch (PostgresException) { rejected = true; }
            Check(rejected && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 119
                && p.Entity.Level == 119 && p.Entity.Strength == 100 && p.Entity.Atributes == 30,
                "failed planned reward transaction retains the earlier level, base stats and unspent points");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                { Call(ProgressionStore, null, "Persist", c, t, p, after); t.Commit(); }
            }
            Call(ProgressionStore, null, "ApplyCommitted", p, after);
        }
        Check(p.Entity.Level == 121 && p.Entity.Experience == 0 && p.Entity.Strength == expected[2]
            && p.Entity.Agility == expected[3] && p.Entity.Vitality == expected[4] && p.Entity.Spirit == expected[5]
            && p.Entity.Atributes == 30 + Config.AttributePointsPerLevel && EntityTable.SaveEntity(p),
            "planned base-stat replacement and earned free points commit together and survive the normal save");
        using (var query = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
        using (var reader = new MySqlReader(query))
        {
            bool same = reader.Read();
            string[] fields = { "level", "experience", "strength", "agility", "vitality", "spirit", "atributes",
                "class", "reborn", "firstrebornclass", "secondrebornclass", "firstrebornlevel", "secondrebornlevel" };
            for (int i = 0; same && i < fields.Length; i++)
                same &= reader.ReadNullableInt64Strict(fields[i]) == expected[i];
            Check(same, "production reader recovers every field of the planned committed profile");
        }
    }
}
