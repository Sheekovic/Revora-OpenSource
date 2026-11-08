using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features.Reincarnation;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type LifeRecovery = typeof(Entity).Assembly.GetType("Revora.Game.Features.ReincarnationRecovery");
    static GameState PendingFirstLife(Map town, byte original = 139, ulong saved = 5000000000UL, byte destination = 11) {
        var p = RebirthPlayer(town, 15, original); p.Entity.Experience = saved;
        if (!EntityTable.SaveEntity(p)) throw new Exception("First-life source save failed.");
        RebirthOffer(p, destination); StoneNpc(p, 214);
        if (Fenced(p) || p.Entity.Reborn != 1 || Rebirths(p) != 1) throw new Exception("First-life fixture rite failed.");
        p.Entity.Level = 129; p.Entity.Experience = DataHolder.LevelExperience(129) - 1;
        p.Entity.Atributes = checked((ushort)(p.Entity.Atributes + 114 * Config.AttributePointsPerLevel));
        if (!EntityTable.SaveEntity(p)) throw new Exception("First-life boundary save failed."); return p;
    }
    static long LifePending(GameState p) { return Scalar("SELECT count(*) FROM revora_first_rebirths WHERE uid=@uid AND old_level>=130 AND recovered_at IS NULL", p.Entity.UID); }
    static ReincarnateInfo LifeSnapshot(GameState p) { lock (p.ItemSyncRoot) return (ReincarnateInfo)Call(LifeRecovery, null, "Snapshot", p); }
    static object LifePlan(GameState p, ulong award, byte cap) {
        lock (p.ItemSyncRoot) {
            try { return Activator.CreateInstance(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceRewardPlan"), Instance, null,
                new object[] { Call(ProgressionStore, null, "Capture", p), award, cap, Config.AttributePointsPerLevel, LifeSnapshot(p) }, null); }
            catch (TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
    static bool RecoveryRejected(Action action) {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
        catch (OverflowException) { return true; }
        catch (Npgsql.PostgresException) { return true; }
    }
    static void VerifyFirstRecovery(string runtime, Map town) {
        VerifyFirstRebirth(runtime, town);
        var p = PendingFirstLife(town); ushort before = p.Entity.Atributes;
        Check(LifeSnapshot(p).Experience == 5000000000UL && LifePending(p) == 1 && p.Account.State == AccountTable.AccountState.Player,
            "ordinary committed rite preserves high-bit former EXP as an unconsumed recovery source");
        p.IncreaseExperience(0, false);
        Check(p.Entity.Level == 129 && LifePending(p) == 1 && p.Entity.Atributes == before, "first-life recovery waits for the exact level-130 boundary");
        var plan = LifePlan(p, 1, 140); var steps = (Array)plan.GetType().GetField("Steps", Instance).GetValue(plan);
        Check(RewardProfile(plan)[0] == 139 && RewardProfile(plan)[1] == 5000000000L
            && steps.Cast<object>().Sum(s => (long)(uint)s.GetType().GetField("MentorExperience", Instance).GetValue(s)) == 260,
            "recovery plan credits the earned level 130 and gives no mentor credit for restored levels");
        p.IncreaseExperience(1, false);
        Check(!Fenced(p) && p.Entity.Level == 139 && p.Entity.Experience == 5000000000UL && p.Entity.Atributes == before + 10 * Config.AttributePointsPerLevel
            && LifePending(p) == 0 && Rebirths(p) == 1 && EntityTable.SaveEntity(p), "boundary award restores former level, full EXP and every intervening attribute point once");
        Check(Scalar("SELECT old_experience FROM revora_first_rebirths WHERE uid=@uid", p.Entity.UID) == 5000000000L
            && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 139
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='first-rebirth-v1'", p.Entity.UID) == 2,
            "consumption preserves the original rite and does not repeat its gifts or achievements");
        var fresh = ReloadGold(p); ConquerItemTable.LoadItems(fresh); new TitleStorage().Login(fresh); SkillTable.LoadSpells(fresh); SkillTable.LoadProficiencies(fresh);
        ushort points = fresh.Entity.Atributes; int items = fresh.Inventory.Count; fresh.IncreaseExperience(1, false);
        Check(!Fenced(fresh) && fresh.Entity.Level == 139 && fresh.Entity.Experience == 5000000001UL && fresh.Entity.Atributes == points
            && fresh.Inventory.Count == items && LifePending(fresh) == 0, "production reload recognizes consumed recovery and preserves subsequent earned EXP");
        fresh.Entity.Level = 129; fresh.Entity.Experience = DataHolder.LevelExperience(129) - 1; EntityTable.SaveEntity(fresh); fresh.IncreaseExperience(1, false);
        Check(fresh.Entity.Level == 130 && fresh.Entity.Experience == 0 && fresh.Entity.Atributes == points + Config.AttributePointsPerLevel,
            "even a later fixture level reset cannot replay the old level-only first-rebirth shortcut");
        var carry = PendingFirstLife(town); carry.IncreaseExperience(DataHolder.LevelExperience(139) + 124, false);
        Check(!Fenced(carry) && carry.Entity.Level == 140 && carry.Entity.Experience == 5000000123UL && LifePending(carry) == 0,
            "large award spends its remainder on the restored high-level curve without losing saved EXP");
        var equal = PendingFirstLife(town, 130, 1000); equal.Entity.Level = 130; equal.Entity.Experience = 17;
        EntityTable.SaveEntity(equal); points = equal.Entity.Atributes; GeneralData.ReincarnationHash(equal);
        Check(!Fenced(equal) && equal.Entity.Level == 130 && equal.Entity.Experience == 1017 && equal.Entity.Atributes == points && LifePending(equal) == 0,
            "already-ready same-level first-life receipt restores EXP without duplicate level points");
        var capped = PendingFirstLife(town, 140, 1000); capped.Entity.Level = 140; capped.Entity.Experience = 17; EntityTable.SaveEntity(capped);
        capped.IncreaseExperience(99, false);
        Check(!Fenced(capped) && capped.Entity.Level == 140 && capped.Entity.Experience == 1017 && LifePending(capped) == 0,
            "pending recovery can retire at the cap while the ordinary cap policy still rejects the new award");
        var higher = PendingFirstLife(town, 135, 1000); higher.Entity.Level = 139; higher.Entity.Experience = 17; EntityTable.SaveEntity(higher);
        higher.IncreaseExperience(1, false);
        Check(higher.Entity.Level == 139 && higher.Entity.Experience == 18 && LifePending(higher) == 0, "stale lower recovery never downgrades or adds its old EXP to a stronger character");
        var lowerCap = PendingFirstLife(town); before = lowerCap.Entity.Atributes; var values = RewardProfile(LifePlan(lowerCap, 2, 135));
        Check(values[0] == 135 && values[1] == 5000000001L && values[6] == before + 6 * Config.AttributePointsPerLevel,
            "first-life planning respects a lower configured cap and counts each recovered level once");
        var young = PendingFirstLife(town, 129, 1000); young.IncreaseExperience(1, false);
        Check(young.Entity.Level == 130 && young.Entity.Experience == 0 && LifeSnapshot(young) == null, "rebirth below level 130 does not invent a saved-EXP recovery");
        foreach (byte destination in rebirthClasses) {
            var promoted = PendingFirstLife(town, 135, 1000, destination); promoted.Entity.Class = (byte)(destination / 10 * 10 + 5);
            EntityTable.SaveEntity(promoted); ushort attributes = promoted.Entity.Atributes; promoted.IncreaseExperience(1, false);
            Check(!Fenced(promoted) && promoted.Entity.Level == 135 && promoted.Entity.Experience == 1000 && LifePending(promoted) == 0
                && promoted.Entity.Atributes == attributes + 6 * Config.AttributePointsPerLevel,
                "first-life recovery accepts a later promotion in every destination school: " + destination);
        }
        VerifyFirstRecoveryFailures(town);
        VerifyFirstRecoverySources(runtime, town);
    }
    static void VerifyFirstRecoveryFailures(Map town) {
        foreach (string table in new[] { "entities", "skills", "revora_first_rebirths" }) {
            var p = PendingFirstLife(town); ushort attributes = p.Entity.Atributes;
            Query("CREATE FUNCTION suppress_life() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_life BEFORE " + (table == "skills" ? "INSERT" : "UPDATE") + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_life()");
            bool rejected;
            try { rejected = RecoveryRejected(() => p.IncreaseExperience(1, false)); }
            finally { Query("DROP TRIGGER suppress_life ON " + table + ";DROP FUNCTION suppress_life()"); }
            Check(rejected && Fenced(p) && p.Entity.Level == 129 && p.Entity.Atributes == attributes && LifePending(p) == 1
                && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 129, "failed recovery write rolls back progression and keeps the receipt pending: " + table);
            var fresh = ReloadGold(p); SkillTable.LoadSpells(fresh); fresh.IncreaseExperience(1, false);
            Check(!Fenced(fresh) && fresh.Entity.Level == 139 && fresh.Entity.Experience == 5000000000UL && LifePending(fresh) == 0,
                "ordinary production reconnect safely resumes rolled-back recovery: " + table);
        }
        foreach (string change in new[] { "profile", "experience", "consumed", "old-class", "new-class", "same-school" }) {
            var p = PendingFirstLife(town); LifeSnapshot(p);
            string sql = change == "profile" ? "UPDATE entities SET experience=experience+1 WHERE uid=@uid"
                : change == "experience" ? "UPDATE revora_first_rebirths SET old_experience=old_experience+1 WHERE uid=@uid"
                : change == "consumed" ? "UPDATE revora_first_rebirths SET recovered_at=now() WHERE uid=@uid"
                : change == "old-class" ? "UPDATE revora_first_rebirths SET old_class=25 WHERE uid=@uid"
                : change == "same-school" ? "UPDATE revora_first_rebirths SET new_class=15 WHERE uid=@uid" : "UPDATE revora_first_rebirths SET new_class=21 WHERE uid=@uid";
            Query(sql, "uid", (long)p.Entity.UID);
            Check(RecoveryRejected(() => p.IncreaseExperience(1, false)) && Fenced(p) && p.Entity.Level == 129,
                "stale character or receipt preimage rejects recovery atomically: " + change);
        }
        foreach (string change in new[] { "negative-exp", "wide-level", "class", "missing", "invalid-rank" }) {
            var p = PendingFirstLife(town);
            Query(change == "missing" ? "DELETE FROM revora_first_rebirths WHERE uid=@uid"
                : "UPDATE revora_first_rebirths SET " + (change == "negative-exp" ? "old_experience=-1" : change == "wide-level" ? "old_level=300" : change == "invalid-rank" ? "new_class=15" : "old_class=25") + " WHERE uid=@uid", "uid", (long)p.Entity.UID);
            Check(RecoveryRejected(() => p.IncreaseExperience(1, false)) && Fenced(p) && p.Entity.Level == 129, "malformed durable recovery is rejected at load: " + change);
        }
        var full = PendingFirstLife(town); full.Entity.Atributes = ushort.MaxValue;
        Check(RecoveryRejected(() => full.IncreaseExperience(1, false)) && full.Entity.Level == 129 && LifePending(full) == 1,
            "attribute overflow fails before recovery consumption");
        var wide = PendingFirstLife(town); Check(RecoveryRejected(() => wide.IncreaseExperience(ulong.MaxValue, false)) && wide.Entity.Level == 129 && LifePending(wide) == 1,
            "unrepresentable EXP fails before recovery mutation");
        var race = PendingFirstLife(town); ushort beforeRaceAttributes = race.Entity.Atributes;
        Parallel.Invoke(() => Parallel.For(0, 100, i => race.IncreaseExperience(1, false)), () => { for (int i = 0; i < 20; i++) EntityTable.SaveEntity(race); });
        Check(!Fenced(race) && EntityTable.SaveEntity(race) && race.Entity.Level == 139 && race.Entity.Experience == 5000000099UL
            && race.Entity.Atributes == beforeRaceAttributes + 10 * Config.AttributePointsPerLevel && LifePending(race) == 0,
            "concurrent ordinary awards and autosaves restore once and preserve all later EXP");
    }
    static void VerifyFirstRecoverySources(string runtime, Map town) {
        Call(Shelby, null, "Initialize"); var market = World(runtime, 1036); MiningNpc(market, 30000);
        foreach (bool fail in new[] { true, false }) {
            var p = PendingFirstLife(town); p.Entity.Teleport(1036, market.Npcs[30000].X, market.Npcs[30000].Y); p.VirtuePoints = 20000;
            EntityTable.SaveEntity(p); LoadVirtue(p); ulong award = p.ExpBall; ShelbyNpc(p, 11);
            if (fail) Query("CREATE FUNCTION suppress_life() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_life BEFORE UPDATE ON revora_first_rebirths FOR EACH ROW EXECUTE FUNCTION suppress_life()");
            try { ShelbyNpc(p, 8); } finally { if (fail) Query("DROP TRIGGER suppress_life ON revora_first_rebirths;DROP FUNCTION suppress_life()"); }
            Check(fail ? Fenced(p) && SavedVirtue(p) == 20000 && LifePending(p) == 1 && ShelbyReceipts(p) == 0
                : !Fenced(p) && SavedVirtue(p) == 5000 && LifePending(p) == 0 && ShelbyReceipts(p) == 1 && p.Entity.Level == 139 && p.Entity.Experience == 5000000000UL + award - 1,
                "paid Shelby experience and first-life recovery share one transaction: failure=" + fail);
        }
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        var mentor = StoredProgressionPlayer(town); var learner = PendingFirstLife(town); MentorLink(mentor, learner, 0); learner.IncreaseExperience(1, false);
        Check(!Fenced(learner) && MentorBalance(learner) == 260, "real mentor credit excludes the nine restored first-life levels");
        foreach (bool fail in new[] { true, false }) {
            var p = PendingFirstLife(town); var donor = StoredProgressionPlayer(town); MentorLink(p, donor, 600); ulong award = p.ExpBall;
            if (fail) Query("CREATE FUNCTION suppress_life() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_life BEFORE UPDATE ON revora_first_rebirths FOR EACH ROW EXECUTE FUNCTION suppress_life()");
            bool rejected = false;
            try { rejected = RecoveryRejected(() => ClaimMentor(p)); } finally { if (fail) Query("DROP TRIGGER suppress_life ON revora_first_rebirths;DROP FUNCTION suppress_life()"); }
            Check(fail ? rejected && Fenced(p) && MentorBalance(donor) == 600 && LifePending(p) == 1
                : !rejected && !Fenced(p) && MentorBalance(donor) == 0 && LifePending(p) == 0 && p.Entity.Level == 139 && p.Entity.Experience == 5000000000UL + award - 1,
                "native mentor reward and first-life recovery commit or roll back together: failure=" + fail);
        }
        Query("CREATE TABLE revora_dis_completions(uid bigint NOT NULL,week varchar(8) NOT NULL,run uuid UNIQUE NOT NULL,elapsed_seconds bigint NOT NULL,completed_at timestamptz NOT NULL,PRIMARY KEY(uid,week))");
        disNow = DateTime.UtcNow; Dis.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => disNow));
        Call(Relics, null, "Initialize"); Call(DarkHorns, null, "Initialize"); Call(DisWorld, null, "PlaceSaint");
        foreach (bool fail in new[] { true, false }) {
            var p = PendingFirstLife(town); var horn = EarnedHorn(p); ulong award = p.ExpBall * 5; HornNpc(p, 5);
            if (fail) Query("CREATE FUNCTION suppress_life() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_life BEFORE UPDATE ON revora_first_rebirths FOR EACH ROW EXECUTE FUNCTION suppress_life()");
            try { HornNpc(p, 50); } finally { if (fail) Query("DROP TRIGGER suppress_life ON revora_first_rebirths;DROP FUNCTION suppress_life()"); }
            Check(fail ? Fenced(p) && p.Inventory.ContainsUID(horn.UID) && LifePending(p) == 1 && HornClaims(p) == 0
                : !Fenced(p) && !p.Inventory.ContainsUID(horn.UID) && LifePending(p) == 0 && HornClaims(p) == 1 && p.Entity.Level == 139 && p.Entity.Experience == 5000000000UL + award - 1,
                "earned Dark Horn and first-life recovery commit or roll back together: failure=" + fail);
        }
    }
}
