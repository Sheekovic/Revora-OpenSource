using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type DarkHorns = typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityDarkHorn");
    static long HornClaims(GameState p) { return Scalar("SELECT count(*) FROM revora_dis_horn_claims WHERE uid=@uid", p.Entity.UID); }
    static ConquerItem EarnedHorn(GameState p)
    {
        var run = Guid.NewGuid(); var horn = Pack(p, 790001, 1); horn.Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)horn.UID);
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open(); using (var t = c.BeginTransaction())
            {
                using (var q = new Npgsql.NpgsqlCommand("INSERT INTO revora_dis_completions(uid,week,run,elapsed_seconds,completed_at) VALUES(@uid,@week,@run,500,@now)", c, t))
                {
                    q.Parameters.AddWithValue("uid", (long)p.Entity.UID); q.Parameters.AddWithValue("week", run.ToString("N").Substring(0, 8));
                    q.Parameters.AddWithValue("run", run); q.Parameters.AddWithValue("now", new DateTimeOffset(disNow)); q.ExecuteNonQuery();
                }
                Call(Relics, null, "Issue", c, t, p.Entity.UID, run, horn); t.Commit();
            }
        }
        return horn;
    }
    static void HornNpc(GameState p, byte option)
    {
        p.ActiveNpc = 3215; p.Entity.X = p.Map.Npcs[3215].X; p.Entity.Y = p.Map.Npcs[3215].Y;
        Npcs.GetDialog(new NpcRequest(true) { NpcID = 3215, OptionID = option }, p);
    }
    static void VerifyDarkHorn(Map map)
    {
        Query(@"CREATE TABLE revora_dis_completions(uid bigint NOT NULL,week varchar(8) NOT NULL,run uuid UNIQUE NOT NULL,
elapsed_seconds bigint NOT NULL,completed_at timestamptz NOT NULL,PRIMARY KEY(uid,week))");
        disNow = new DateTime(2026, 9, 15, 20, 0, 0, DateTimeKind.Utc);
        Dis.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => disNow));
        Call(Relics, null, "Initialize"); Call(DarkHorns, null, "Initialize"); Call(DisWorld, null, "PlaceSaint");
        var p = StoredProgressionPlayer(map); var horn = EarnedHorn(p);
        HornNpc(p, 50); Check(HornClaims(p) == 0 && p.Inventory.ContainsUID(horn.UID), "forged Horn choice cannot bypass a presented item offer");
        ItemHandler.UseItem(horn, p);
        Check(p.Inventory.ContainsUID(horn.UID) && HornClaims(p) == 0, "Dark Horn right-click explains its NPC exchange without consuming the item");
        HornNpc(p, 5); disNow = disNow.AddMinutes(2); HornNpc(p, 50);
        Check(HornClaims(p) == 0, "expired Dark Horn offer cannot be redeemed");
        horn.Lock = 1; Query("UPDATE items SET locked=1 WHERE uid=@uid", "uid", (long)horn.UID); HornNpc(p, 5); HornNpc(p, 50);
        Check(HornClaims(p) == 0 && p.Inventory.ContainsUID(horn.UID), "locked Dark Horn cannot be offered or spent");
        horn.Lock = 0; Query("UPDATE items SET locked=0 WHERE uid=@uid", "uid", (long)horn.UID);
        var mentor = StoredProgressionPlayer(map); MentorLink(mentor, p, 0);
        p.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(p);
        ulong award = ExpScale(p.ExpBall, 5, 1); long[] expected = RewardProfile(RewardPlan(p, award));
        Query("ALTER TABLE revora_dis_horn_claims ADD CONSTRAINT fail_horn_claim CHECK(false) NOT VALID");
        HornNpc(p, 5); HornNpc(p, 50);
        Check(Fenced(p) && HornClaims(p) == 0 && p.Inventory.ContainsUID(horn.UID) && p.Entity.Level == 135 && SkillRows(p) == 0
            && MentorBalance(p) == 0 && Scalar("SELECT count(*) FROM revora_dis_relics WHERE uid=@uid AND used_at IS NOT NULL", p.Entity.UID) == 0,
            "failed Horn receipt rolls back item, level, skill and mentor contribution together");
        Query("ALTER TABLE revora_dis_horn_claims DROP CONSTRAINT fail_horn_claim"); DemonReset(p); LoadProgressionImage(p);
        while (p.Inventory.Count < 40) Pack(p, 1088001, 1);
        HornNpc(p, 5);
        Parallel.Invoke(() => { for (int n = 0; n < 12; n++) Npcs.GetDialog(new NpcRequest(true) { NpcID = 3215, OptionID = 50 }, p); },
            () => { for (int n = 0; n < 12; n++) EntityTable.SaveEntity(p); });
        Check(!Fenced(p) && HornClaims(p) == 1 && !p.Inventory.ContainsUID(horn.UID) && p.Inventory.Count == 39
            && p.Entity.Level == expected[0] && p.Entity.Experience == (ulong)expected[1] && p.Entity.Atributes == expected[6]
            && SkillRows(p) == 1 && MentorBalance(p) > 0
            && Scalar("SELECT experience FROM revora_dis_horn_claims WHERE uid=@uid", p.Entity.UID) == (long)award,
            "ordinary full-bag Horn exchange commits five balls worth of direct EXP with level effects once under concurrent saves and requests");
        LoadProgressionImage(p); Revora.Database.KnownPersons.LoadMentor(mentor); SkillTable.LoadSpells(p);
        Check(p.Entity.Experience == (ulong)expected[1] && p.Spells[1110].Available && mentor.PrizeExperience == (ulong)MentorBalance(p)
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == expected[1],
            "production progression, skill and mentor loaders retain the full committed Horn reward");
        var next = EarnedHorn(p); HornNpc(p, 50);
        Check(HornClaims(p) == 1 && p.Inventory.ContainsUID(next.UID), "replayed Horn choice cannot consume the next earned item");

        var unrelated = StoredProgressionPlayer(map); var fake = Pack(unrelated, 790001, 1); fake.Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)fake.UID); HornNpc(unrelated, 5); HornNpc(unrelated, 50);
        Check(!Fenced(unrelated) && HornClaims(unrelated) == 0 && unrelated.Inventory.ContainsUID(fake.UID), "unissued lookalike Horn cannot purchase experience");
        var max = StoredProgressionPlayer(map); var saved = EarnedHorn(max); max.Entity.Level = Config.LevelCap;
        HornNpc(max, 5); HornNpc(max, 50);
        Check(HornClaims(max) == 0 && max.Inventory.ContainsUID(saved.UID), "maximum-level character keeps its Dark Horn instead of spending it for no EXP");
        var cursed = StoredProgressionPlayer(map); var cursedHorn = EarnedHorn(cursed);
        cursed.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed, 60, false); HornNpc(cursed, 5); HornNpc(cursed, 50);
        Check(HornClaims(cursed) == 0 && cursed.Inventory.ContainsUID(cursedHorn.UID) && cursed.Entity.Experience == 5000,
            "cursed character keeps its Horn and does not receive unusable EXP");
        var moved = StoredProgressionPlayer(map); var movedHorn = EarnedHorn(moved); HornNpc(moved, 5);
        moved.Entity.X = 1; moved.Entity.Y = 1;
        Npcs.GetDialog(new NpcRequest(true) { NpcID = 3215, OptionID = 50 }, moved);
        Check(HornClaims(moved) == 0 && moved.Inventory.ContainsUID(movedHorn.UID), "leaving Solar Saint invalidates the physical exchange conditions");
        HornNpc(moved, 5); Query("UPDATE items SET entityid=@owner WHERE uid=@uid", "uid", (long)movedHorn.UID, "owner", (long)unrelated.Entity.UID);
        HornNpc(moved, 50);
        Check(HornClaims(moved) == 0 && moved.Entity.Experience == 5000, "changed durable Horn owner prevents a stale inventory exchange");
        var stale = StoredProgressionPlayer(map); var staleHorn = EarnedHorn(stale); HornNpc(stale, 5);
        Query("UPDATE entities SET experience=experience+1 WHERE uid=@uid", "uid", (long)stale.Entity.UID); HornNpc(stale, 50);
        Check(Fenced(stale) && HornClaims(stale) == 0 && stale.Inventory.ContainsUID(staleHorn.UID)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=790001", stale.Entity.UID) == 1,
            "changed durable character profile rolls back Horn consumption and requires reload");

        var restored = PendingReincarnation(map); var recoveryHorn = EarnedHorn(restored);
        var planned = RewardProfile(RecoveryPlan(restored, ExpScale(restored.ExpBall, 5, 1)));
        HornNpc(restored, 5); HornNpc(restored, 50);
        Check(!Fenced(restored) && HornClaims(restored) == 1 && !restored.Inventory.ContainsUID(recoveryHorn.UID)
            && restored.Entity.Level == planned[0] && restored.Entity.Experience == (ulong)planned[1] && PendingRows(restored) == 0,
            "Horn award crossing level 110 restores reincarnation EXP and consumes its snapshot in the same exchange");
    }
}
