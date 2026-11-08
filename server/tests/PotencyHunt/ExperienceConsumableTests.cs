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
    static readonly Type ExpItems = typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceConsumables");
    static DateTime ballNow = DateTime.Now.Date.AddHours(12);
    static GameState BallPlayer(Map map, byte count = 0) {
        var p = StoredProgressionPlayer(map); p.LastResetTime = ballNow; p.ExpBalls = count;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Ball fixture save failed."); return p;
    }
    static long BallCount(GameState p) { return Scalar("SELECT COALESCE(sum(used),0) FROM revora_exp_ball_days WHERE uid=@uid", p.Entity.UID); }
    static GameState ReloadBall(GameState p) {
        p = ReloadGold(p); ConquerItemTable.LoadItems(p); SkillTable.LoadSpells(p); return p;
    }
    static void VerifyExpItems(string runtime, Map town) {
        VerifyFirstRebirth(runtime, town);
        ExpItems.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => ballNow));
        foreach (uint id in new uint[] {723700,723911,722136,723744,722057}) foreach (bool bound in new[] {false,true}) {
            var p = BallPlayer(town); var item = Pack(p,id,2); item.Bound = bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid", "bound", bound ? 1 : 0, "uid", (long)item.UID);
            bool standard = id != 723744 && id != 722057;
            ulong award = standard ? p.ExpBall : DataHolder.LevelExperience(135) / 100 * 10;
            var other = Pack(p,id,1); ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && p.Entity.Experience == 5000 + award && item.StackSize == 1 && SavedRune(item,"stacksize") == 1
                && p.Inventory.ContainsUID(other.UID) && p.ExpBalls == (standard ? 1 : 0) && BallCount(p) == (standard ? 1 : 0)
                && Scalar("SELECT experience FROM entities WHERE uid=@uid",p.Entity.UID) == (long)(5000 + award),
                "ordinary item route atomically grants exact EXP, debits one selected unit and counts only standard balls: " + id + "/" + bound);
            uint uid = item.UID; p = ReloadBall(p); var loaded = p.Inventory.Objects.Single(i => i.UID == uid);
            ItemHandler.UseItem(loaded,p); ItemHandler.UseItem(loaded,p);
            Check(!Fenced(p) && p.Entity.Experience == 5000 + award * 2 && !p.Inventory.ContainsUID(uid)
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",uid) == 0 && BallCount(p) == (standard ? 2 : 0),
                "production reconnect retains reward and allowance; deleted item replay grants no EXP: " + id + "/" + bound);
        }
        var daily = BallPlayer(town); var balls = Pack(daily,722136,12); ulong each = daily.ExpBall;
        Parallel.For(0,48,i => { if (i % 3 == 0) EntityTable.SaveEntity(daily); else ItemHandler.UseItem(balls,daily); });
        Check(!Fenced(daily) && BallCount(daily) == 10 && daily.ExpBalls == 10 && balls.StackSize == 2
            && daily.Entity.Experience == 5000 + each * 10 && SavedRune(balls,"stacksize") == 2,
            "concurrent standard ball use and autosave stop at ten durable uses without lost rewards");
        daily.ExpBalls = 0; EntityTable.SaveEntity(daily); daily = ReloadBall(daily); balls = daily.Inventory.Objects.Single();
        ItemHandler.UseItem(balls,daily);
        Check(!Fenced(daily) && BallCount(daily) == 10 && balls.StackSize == 2, "legacy mirror reset and reconnect cannot refill an already spent allowance");
        ballNow = ballNow.AddDays(1); ItemHandler.UseItem(balls,daily);
        Check(!Fenced(daily) && BallCount(daily) == 1 && balls.StackSize == 1 && daily.ExpBalls == 1, "midnight refreshes the allowance for an online character without relogging");
        ballNow = ballNow.AddDays(-1); ItemHandler.UseItem(balls,daily);
        Check(!Fenced(daily) && BallCount(daily) == 1 && balls.StackSize == 1, "clock rollback cannot reopen yesterday's allowance");
        ballNow = DateTime.Now.Date.AddHours(12);
        foreach (byte old in new byte[] {9,10}) {
            var p = BallPlayer(town,old); var item = Pack(p,723700,2); ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && item.StackSize == (old == 9 ? 1 : 2) && p.ExpBalls == 10,
                "first use imports today's saved legacy counter without granting extra uses: " + old);
        }
        var yesterday = BallPlayer(town,10); yesterday.LastResetTime = ballNow.AddDays(-1); EntityTable.SaveEntity(yesterday);
        ItemHandler.UseItem(Pack(yesterday,723911,1),yesterday);
        Check(!Fenced(yesterday) && yesterday.ExpBalls == 1 && BallCount(yesterday) == 1, "yesterday's legacy limit resets on the first authoritative transaction");
        foreach (string target in new[] {"profile","delete","stack","insert-count","update-count","skills","recovery"}) {
            bool recovery = target == "recovery"; var p = recovery ? PendingFirstLife(town) : BallPlayer(town);
            if (target == "skills") { p.Entity.Experience = DataHolder.LevelExperience(135)-1; EntityTable.SaveEntity(p); }
            if (target == "update-count") ItemHandler.UseItem(Pack(p,722136,1),p);
            ulong before = p.Entity.Experience; byte level = p.Entity.Level; long count = BallCount(p);
            var item = Pack(p,722136,(ushort)(target == "stack" ? 2 : 1));
            string table = target == "profile" ? "entities" : target == "delete" || target == "stack" ? "items"
                : target == "skills" ? "skills" : recovery ? "revora_first_rebirths" : "revora_exp_ball_days";
            string verb = target == "delete" ? "DELETE" : target == "insert-count" || target == "skills" ? "INSERT" : "UPDATE";
            Query("CREATE FUNCTION suppress_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ball BEFORE " + verb + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_ball()");
            ItemHandler.UseItem(item,p);
            Check(Fenced(p) && p.Entity.Experience == before && p.Entity.Level == level && BallCount(p) == count
                && SavedRune(item,"stacksize") == item.StackSize && p.Inventory.ContainsUID(item.UID)
                && Scalar("SELECT experience FROM entities WHERE uid=@uid",p.Entity.UID) == (long)before
                && (!recovery || LifePending(p) == 1), "suppressed " + target + " write rolls back item, allowance, progression and recovery");
            Query("DROP TRIGGER suppress_ball ON " + table + ";DROP FUNCTION suppress_ball()");
            p = ReloadBall(p); var loaded = p.Inventory.Objects.Single(i => i.UID == item.UID); ItemHandler.UseItem(loaded,p);
            Check(!Fenced(p) && BallCount(p) == count + 1 && (!recovery || p.Entity.Level == 139 && LifePending(p) == 0),
                "production reconnect can retry the preserved reward after " + target + " failure");
        }
        foreach (string change in new[] {"entityid=1","stacksize=3","bound=1","locked=1","warehouse=1","position=1","suspicious=1","inwardrobe=1","days='1'"}) {
            var p = BallPlayer(town); var item = Pack(p,722136,2);
            Query("UPDATE items SET " + change + " WHERE uid=@uid","uid",(long)item.UID); ItemHandler.UseItem(item,p);
            Check(Fenced(p) && p.Entity.Experience == 5000 && BallCount(p) == 0 && p.Inventory.ContainsUID(item.UID), "changed durable item preimage cannot receive unpaid EXP: " + change);
        }
        var guarded = BallPlayer(town); var kept = Pack(guarded,722136,1);
        guarded.Trade.InTrade = true; ItemHandler.UseItem(kept,guarded); guarded.Trade.InTrade = false;
        guarded.Fake = true; ItemHandler.UseItem(kept,guarded); guarded.Fake = false;
        guarded.Entity.FullyLoaded = false; ItemHandler.UseItem(kept,guarded); guarded.Entity.FullyLoaded = true;
        kept.Lock = 1; ItemHandler.UseItem(kept,guarded); kept.Lock = 0;
        kept.Suspicious = true; ItemHandler.UseItem(kept,guarded); kept.Suspicious = false;
        kept.Days = 1; kept.DayStamp = ballNow.AddDays(-2); ItemHandler.UseItem(kept,guarded); kept.Days = 0;
        ItemHandler.UseItem(new ConquerItem(true) { UID=kept.UID, ID=kept.ID },guarded);
        Check(!Fenced(guarded) && guarded.Entity.Experience == 5000 && BallCount(guarded) == 0 && guarded.Inventory.ContainsUID(kept.UID),
            "trade, fake actors, incomplete login, locks, suspicious items, expiry and substituted references preserve payment");
        ItemHandler.UseItem(kept,guarded); Check(!Fenced(guarded) && BallCount(guarded) == 1, "temporary restrictions ending permit normal use");
        foreach (bool standard in new[] {true,false}) {
            var p = BallPlayer(town); p.Entity.Level = standard ? (byte)137 : Config.LevelCap; EntityTable.SaveEntity(p);
            var item = Pack(p,standard ? 722136u : 722057u,1); ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && p.Entity.Experience == 5000 && p.Inventory.ContainsUID(item.UID) && BallCount(p) == 0,
                "level-limited " + (standard ? "standard" : "power") + " ball is kept without a charge");
        }
        foreach (string restriction in new[] {"curse","death","training"}) {
            var p = BallPlayer(town); var item = Pack(p,722136,1); bool oldTraining = Config.TrainingGroundBlocksExperience;
            if (restriction == "curse") p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed, 60, false);
            if (restriction == "death") p.Entity.Hitpoints = 0;
            if (restriction == "training") { Config.TrainingGroundBlocksExperience = true; p.BindWorldMap(World(runtime,1039).ID); }
            ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && p.Entity.Experience == 5000 && p.Inventory.ContainsUID(item.UID) && BallCount(p) == 0,
                "blocked experience cannot consume its item or allowance: " + restriction);
            Config.TrainingGroundBlocksExperience = oldTraining;
        }
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        foreach (bool bound in new[] {false,true}) {
            var mentor = BallPlayer(town); var child = BallPlayer(town); MentorLink(mentor,child,100);
            child.Entity.Experience = DataHolder.LevelExperience(135)-1; EntityTable.SaveEntity(child);
            var item = Pack(child,722136,1); item.Bound = bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound ? 1 : 0,"uid",(long)item.UID);
            ItemHandler.UseItem(item,child);
            Check(!Fenced(child) && child.Entity.Level == 136 && MentorBalance(child) == (bound ? 100 : 372)
                && BallCount(child) == 1 && !child.Inventory.ContainsUID(item.UID),
                "bound standard balls preserve source exclusion while unbound balls atomically credit earned level to mentor: " + bound);
        }
        var badMentor = BallPlayer(town); var badChild = BallPlayer(town); MentorLink(badMentor,badChild,100);
        badChild.Entity.Experience = DataHolder.LevelExperience(135)-1; EntityTable.SaveEntity(badChild);
        var mentorBall = Pack(badChild,723700,1); ulong original = badChild.Entity.Experience;
        Query("CREATE FUNCTION suppress_ball() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ball BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_ball()");
        ItemHandler.UseItem(mentorBall,badChild);
        Check(Fenced(badChild) && badChild.Entity.Experience == original && BallCount(badChild) == 0 && MentorBalance(badChild) == 100
            && badChild.Inventory.ContainsUID(mentorBall.UID), "mentor credit failure preserves the ball, allowance and whole progression reward");
        Query("DROP TRIGGER suppress_ball ON apprentice;DROP FUNCTION suppress_ball()");
        var life = PendingFirstLife(town); var lifeBall = Pack(life,722136,2); lifeBall.Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)lifeBall.UID);
        long[] expected = RewardProfile(LifePlan(life,life.ExpBall,Config.LevelCap));
        ItemHandler.UseItem(lifeBall,life);
        Check(!Fenced(life) && life.Entity.Level == expected[0] && life.Entity.Experience == (ulong)expected[1]
            && life.Entity.Atributes == expected[6] && LifePending(life) == 0 && BallCount(life) == 1 && lifeBall.StackSize == 1,
            "standard ball at the first-life boundary atomically restores exact level, saved EXP and all points");
        ItemHandler.UseItem(lifeBall,life);
        Check(!Fenced(life) && lifeBall.StackSize == 1 && BallCount(life) == 1 && life.Entity.Experience == (ulong)expected[1],
            "post-recovery level limit preserves remaining standard balls without replaying recovery");
        var legacy = BallPlayer(town); var single = Pack(legacy,722057,0); ItemHandler.UseItem(single,legacy);
        Check(!Fenced(legacy) && !legacy.Inventory.ContainsUID(single.UID) && legacy.Entity.Experience > 5000, "legacy zero-stack singleton remains usable once");
        var timed = BallPlayer(town); var timedBall = Pack(timed,722136,1); timedBall.Days = 1; timedBall.DayStamp = ballNow.AddHours(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid", "stamp",timedBall.DayStamp.ToBinary(),"uid",(long)timedBall.UID);
        ItemHandler.UseItem(timedBall,timed);
        Check(!Fenced(timed) && !timed.Inventory.ContainsUID(timedBall.UID) && BallCount(timed) == 1, "unexpired timed ball matches durable expiry before spending");
    }
}
