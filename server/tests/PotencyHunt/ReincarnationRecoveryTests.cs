using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features.Reincarnation;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static GameState PendingReincarnation(Map map, byte original = 139, ulong savedExp = 5000000000UL)
    {
        var p = StoredProgressionPlayer(map); p.Entity.Level = original; p.Entity.Reborn = 2;
        p.Entity.SecondRebornLevel = original; p.Entity.Experience = savedExp;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Snapshot fixture save failed.");
        ReincarnationTable.NewReincarnated(p.Entity);
        Kernel.ReincarnatedCharacters[p.Entity.UID] = new ReincarnateInfo { UID = p.Entity.UID, Level = original, Experience = savedExp };
        p.Entity.Level = 109; p.Entity.Experience = DataHolder.LevelExperience(109) - 1; p.Entity.Atributes = 30;
        if (!EntityTable.SaveEntity(p)) throw new Exception("Reset fixture save failed.");
        return p;
    }
    static object RecoveryPlan(GameState p, ulong award, byte cap = 140)
    {
        lock (p.ItemSyncRoot)
        {
            var before = Call(ProgressionStore, null, "Capture", p);
            var type = typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceRewardPlan");
            try { return Activator.CreateInstance(type, Instance, null,
                new object[] { before, award, cap, Config.AttributePointsPerLevel, Kernel.ReincarnatedCharacters[p.Entity.UID] }, null); }
            catch (TargetInvocationException ex)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
    }
    static long PendingRows(GameState p)
    { return Scalar("SELECT count(*) FROM reincarnation WHERE uid=@uid", p.Entity.UID); }
    static void VerifyReincarnationRecovery(Map map)
    {
        Query("CREATE TABLE reincarnation(id bigserial PRIMARY KEY,uid bigint NOT NULL,level bigint NOT NULL,experience bigint NOT NULL)");
        var p = PendingReincarnation(map);
        Check(Scalar("SELECT experience FROM reincarnation WHERE uid=@uid", p.Entity.UID) == 5000000000L,
            "new reincarnation snapshot saves actual high-bit EXP instead of zero");
        ReincarnateInfo ignored; Kernel.ReincarnatedCharacters.TryRemove(p.Entity.UID, out ignored);
        ReincarnationTable.Load(); ReincarnationTable.Load();
        Check(Kernel.ReincarnatedCharacters[p.Entity.UID].Experience == 5000000000UL,
            "production reincarnation loader retains full signed-bigint EXP and can reload the same snapshot");
        bool rejected = false;
        p.Entity.Level = 139;
        try { ReincarnationTable.NewReincarnated(p.Entity); } catch (InvalidOperationException) { rejected = true; }
        p.Entity.Level = 109;
        Check(rejected && PendingRows(p) == 1 && Scalar("SELECT experience FROM reincarnation WHERE uid=@uid", p.Entity.UID) == 5000000000L,
            "repeat snapshot capture cannot replace or duplicate a pending recovery");
        p.IncreaseExperience(0, false);
        Check(p.Entity.Level == 109 && PendingRows(p) == 1, "reincarnation cannot restore before earning level 110");
        p.IncreaseExperience(1, false);
        Check(p.Entity.Level == 139 && p.Entity.Experience == 5000000000UL
            && p.Entity.Atributes == 30 + (139 - 109) * Config.AttributePointsPerLevel,
            "level-110 crossing restores saved EXP without caller overwrite and recovers each missing level's points once");
        Check(PendingRows(p) == 0 && !Kernel.ReincarnatedCharacters.ContainsKey(p.Entity.UID)
            && Scalar("SELECT level FROM entities WHERE uid=@uid", p.Entity.UID) == 139
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == 5000000000L
            && EntityTable.SaveEntity(p), "restored progression and consumed snapshot commit together and survive the normal save");
        ushort points = p.Entity.Atributes;
        GeneralData.ReincarnationHash(p); Handle.Hash(p); p.IncreaseExperience(1, false);
        Check(p.Entity.Level == 139 && p.Entity.Experience == 5000000001UL && p.Entity.Atributes == points,
            "both legacy restoration entry points and subsequent awards cannot replay consumed recovery");

        Query("INSERT INTO reincarnation(uid,level,experience) VALUES(4294967000,139,-1)");
        rejected = false; try { ReincarnationTable.Load(); } catch (OverflowException) { rejected = true; }
        Query("DELETE FROM reincarnation WHERE uid=4294967000");
        Check(rejected && !Kernel.ReincarnatedCharacters.ContainsKey(4294967000), "negative stored EXP is rejected instead of loading a zero recovery");
        Query("INSERT INTO reincarnation(uid,level,experience) VALUES(4294967000,139,5),(4294967000,139,5)");
        rejected = false; try { ReincarnationTable.Load(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Scalar("SELECT count(*) FROM reincarnation WHERE uid=@uid", 4294967000) == 2,
            "duplicate legacy snapshots fail loading instead of choosing a receipt or deleting evidence");
        Query("DELETE FROM reincarnation WHERE uid=4294967000"); Kernel.ReincarnatedCharacters.TryRemove(4294967000, out ignored);

        var carry = PendingReincarnation(map); carry.Entity.Experience = 0;
        carry.IncreaseExperience(DataHolder.LevelExperience(109) + DataHolder.LevelExperience(139) + 123, false);
        Check(carry.Entity.Level == 140 && carry.Entity.Experience == 5000000123UL
            && carry.Entity.Atributes == 30 + (140 - 109) * Config.AttributePointsPerLevel,
            "one large award restores at 110 then spends remaining EXP on the restored high-level curve");
        var ready = PendingReincarnation(map); ready.Entity.Level = 110; ready.Entity.Experience = 17;
        var plan = RecoveryPlan(ready, 3); var values = RewardProfile(plan);
        var steps = (Array)plan.GetType().GetField("Steps", Instance).GetValue(plan);
        Check(values[0] == 139 && values[1] == 5000000020L && values[6] == 30 + 29 * Config.AttributePointsPerLevel
            && steps.Cast<object>().All(step => (uint)step.GetType().GetField("MentorExperience", Instance).GetValue(step) == 0),
            "already-ready restoration retains pending EXP and produces no mentor credit for recovered levels");
        GeneralData.ReincarnationHash(ready);
        Check(ready.Entity.Level == 139 && ready.Entity.Experience == 5000000017UL && PendingRows(ready) == 0,
            "legacy general restoration now uses the shared durable result");
        var alternate = PendingReincarnation(map); alternate.Entity.Level = 110; alternate.Entity.Experience = 19;
        Handle.Hash(alternate);
        Check(alternate.Entity.Level == 139 && alternate.Entity.Experience == 5000000019UL && PendingRows(alternate) == 0,
            "legacy feature restoration uses the same durable result");
        var cap = PendingReincarnation(map);
        values = RewardProfile(RecoveryPlan(cap, 2, 135));
        Check(values[0] == 135 && values[1] == 5000000001L && values[6] == 30 + 26 * Config.AttributePointsPerLevel,
            "restoration respects the configured cap and retains saved plus unspent EXP at that cap");
        var minimum = PendingReincarnation(map, 110, 1000);
        minimum.IncreaseExperience(2, false);
        Check(minimum.Entity.Level == 110 && minimum.Entity.Experience == 1001 && PendingRows(minimum) == 0
            && minimum.Entity.Atributes == 30 + Config.AttributePointsPerLevel,
            "a saved level-110 snapshot restores EXP without negative or duplicate point arithmetic");
        var stale = PendingReincarnation(map, 135); stale.Entity.Level = 139; stale.Entity.Experience = 17;
        GeneralData.ReincarnationHash(stale);
        Check(stale.Entity.Level == 139 && stale.Entity.Experience == 17 && stale.Entity.Atributes == 30 && PendingRows(stale) == 0,
            "stale legacy snapshot cannot downgrade a stronger character or mint its EXP again");
        var full = PendingReincarnation(map); full.Entity.Atributes = ushort.MaxValue;
        rejected = false; try { full.IncreaseExperience(1, false); } catch (OverflowException) { rejected = true; }
        Check(rejected && full.Entity.Level == 109 && full.Entity.Atributes == ushort.MaxValue && PendingRows(full) == 1,
            "restored point overflow fails before consuming the snapshot or changing the level");
        var wide = PendingReincarnation(map); wide.Entity.Experience = 0;
        rejected = false; try { wide.IncreaseExperience(ulong.MaxValue, false); } catch (OverflowException) { rejected = true; }
        Check(rejected && wide.Entity.Level == 109 && wide.Entity.Experience == 0 && PendingRows(wide) == 1,
            "unrepresentable restored EXP fails before durable or cached mutation");

        var failure = PendingReincarnation(map);
        Query(@"CREATE FUNCTION reject_reincarnation_delete() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture delete failure'; END $$;
CREATE TRIGGER reject_reincarnation_delete BEFORE DELETE ON reincarnation FOR EACH ROW EXECUTE FUNCTION reject_reincarnation_delete()");
        rejected = false;
        try { failure.IncreaseExperience(1, false); } catch (Npgsql.PostgresException) { rejected = true; }
        finally { Query("DROP TRIGGER reject_reincarnation_delete ON reincarnation; DROP FUNCTION reject_reincarnation_delete()"); }
        Check(rejected && Fenced(failure) && failure.Entity.Level == 109 && failure.Entity.Atributes == 30
            && failure.Entity.Experience == DataHolder.LevelExperience(109) - 1 && !failure.Spells.ContainsKey(1110)
            && Scalar("SELECT level FROM entities WHERE uid=@uid", failure.Entity.UID) == 109 && PendingRows(failure) == 1,
            "failed snapshot deletion rolls back profile progression and fences before class skill or cache effects");
        DemonReset(failure); LoadProgressionImage(failure); failure.IncreaseExperience(1, false);
        Check(!Fenced(failure) && failure.Entity.Level == 139 && failure.Entity.Experience == 5000000000UL && PendingRows(failure) == 0,
            "reloaded unchanged profile can recover successfully after a rolled-back transaction");
        var conflict = PendingReincarnation(map);
        Query("UPDATE entities SET experience=experience+1 WHERE uid=@uid", "uid", (long)conflict.Entity.UID);
        rejected = false; try { conflict.IncreaseExperience(1, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Fenced(conflict) && conflict.Entity.Level == 109 && PendingRows(conflict) == 1
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", conflict.Entity.UID) == (long)DataHolder.LevelExperience(109),
            "recovery cannot overwrite externally changed character progression");
        var changed = PendingReincarnation(map);
        Query("UPDATE reincarnation SET experience=6000000000 WHERE uid=@uid", "uid", (long)changed.Entity.UID);
        rejected = false; try { changed.IncreaseExperience(1, false); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Fenced(changed) && changed.Entity.Level == 109 && PendingRows(changed) == 1
            && Scalar("SELECT level FROM entities WHERE uid=@uid", changed.Entity.UID) == 109,
            "changed snapshot preimage rolls back the entire recovery profile");
        var race = PendingReincarnation(map); bool[] saves = new bool[20];
        Parallel.Invoke(() => Parallel.For(0, 100, i => race.IncreaseExperience(1, false)),
            () => { for (int i = 0; i < saves.Length; i++) saves[i] = EntityTable.SaveEntity(race); });
        Check(saves.All(value => value) && !Fenced(race) && EntityTable.SaveEntity(race)
            && race.Entity.Level == 139 && race.Entity.Experience == 5000000099UL && PendingRows(race) == 0
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", race.Entity.UID) == 5000000099L,
            "concurrent awards and saves restore once and retain every additional EXP point");
        var pendingRequest = PendingReincarnation(map); uint cp = pendingRequest.Entity.ConquerPoints;
        Revora.Network.GamePackets.Reincarnation.Process(ReincarnationRequest(), pendingRequest);
        Check(PendingRows(pendingRequest) == 1 && pendingRequest.Entity.ConquerPoints == cp && pendingRequest.Entity.Level == 109,
            "an ineligible repeat reincarnation request preserves the pending record and CP balance");
        var projection = PendingReincarnation(map); var spells = projection.Spells; projection.Spells = null;
        rejected = false;
        try { projection.IncreaseExperience(1, false); } catch (InvalidOperationException) { rejected = true; }
        finally { projection.Spells = spells; }
        Check(rejected && Fenced(projection) && PendingRows(projection) == 1
            && Kernel.ReincarnatedCharacters.ContainsKey(projection.Entity.UID)
            && Scalar("SELECT level FROM entities WHERE uid=@uid", projection.Entity.UID) == 109,
            "missing skill cache is rejected inside the transaction before committing recovery");
        projection.IncreaseExperience(1, false);
        Check(projection.Entity.Level == 109 && Scalar("SELECT experience FROM entities WHERE uid=@uid", projection.Entity.UID) == (long)DataHolder.LevelExperience(109) - 1,
            "fenced missing-skill cache cannot earn against or overwrite its saved profile");
        VerifyReincarnationRequestBounds(map);
    }
    static byte[] ReincarnationRequest()
    {
        var packet = new byte[21]; Writer.WriteUInt16(13, 0, packet); Writer.WriteUInt16(1066, 2, packet);
        packet[8] = 11; packet[12] = 3; Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal, 13, packet); return packet;
    }
    static void VerifyReincarnationRequestBounds(Map map)
    {
        var p = PendingReincarnation(map); p.Entity.Level = 110; p.Entity.Experience = 0;
        var valid = ReincarnationRequest();
        Revora.Network.GamePackets.Reincarnation.Process(null, p);
        for (int length = 0; length < valid.Length; length++)
            Revora.Network.GamePackets.Reincarnation.Process(valid.Take(length).ToArray(), p);
        var wrongType = (byte[])valid.Clone(); Writer.WriteUInt16(1065, 2, wrongType);
        Revora.Network.GamePackets.Reincarnation.Process(wrongType, p);
        var wrongLength = (byte[])valid.Clone(); Writer.WriteUInt16(12, 0, wrongLength);
        Revora.Network.GamePackets.Reincarnation.Process(wrongLength, p);
        var wrongSeal = (byte[])valid.Clone(); wrongSeal[20] ^= 1;
        Revora.Network.GamePackets.Reincarnation.Process(wrongSeal, p);
        Check(p.Entity.Level == 110 && p.Entity.Experience == 0 && PendingRows(p) == 1,
            "malformed reincarnation frames cannot trigger recovery or other request side effects");
        Kernel.GamePool.Remove(p.Entity.UID);
        Revora.Network.GamePackets.Reincarnation.Process(valid, p); Kernel.GamePool[p.Entity.UID] = p;
        Check(p.Entity.Level == 110 && PendingRows(p) == 1, "detached sessions cannot trigger reincarnation recovery through the request handler");
        p.Trade.InTrade = true; Revora.Network.GamePackets.Reincarnation.Process(valid, p); p.Trade.InTrade = false;
        Check(p.Entity.Level == 110 && PendingRows(p) == 1, "reincarnation requests cannot reset a character during a trade");
        var below = StoredProgressionPlayer(map); below.Entity.Level = 109; below.Entity.Reborn = 2;
        below.Entity.ConquerPoints = Config.SerethReincarnationCp; var dew = Pack(below, 711083, 1);
        uint cp = below.Entity.ConquerPoints; ushort body = below.Entity.Body;
        Revora.Network.GamePackets.Reincarnation.Process(valid, below);
        Check(below.Entity.Level == 109 && below.Entity.ConquerPoints == cp && below.Entity.Body == body
            && below.Inventory.ContainsUID(dew.UID) && PendingRows(below) == 0,
            "request below level 110 without an owned offer cannot charge CPs, consume dew or change the body");
    }
}
