using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type DisRanks = typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityRanking");
    static long RankClaims(GameState p) { return Scalar("SELECT count(*) FROM revora_dis_rank_prizes WHERE uid=@uid AND claimed_at IS NOT NULL", p.Entity.UID); }
    static Guid SeedRankFinish(GameState p, TimeSpan elapsed, int difficulty=0)
    {
        var run = Guid.NewGuid(); var started = disNow - elapsed;
        Query(@"INSERT INTO revora_dis_runs(uid,run,week,phase,stage,progress,quota,step,guard,mirror,token,items,crystal_order,started_at,star_sword)
VALUES(@uid,@run,@week,'victory',4,4,400,1,false,0,0,ARRAY[]::bigint[],ARRAY[0,1,2,3,4,5,6,7,8],@started,false)",
            "uid", (long)p.Entity.UID, "run", run, "week", "20260928", "started", new DateTimeOffset(started));
        Query(@"INSERT INTO revora_dis_steps(run,step,uid,stage,progress,action,monster_uid,epoch)
VALUES(@run,1,@uid,4,4,'defeat',9914004,@epoch)", "run", run, "uid", (long)p.Entity.UID, "epoch", Guid.NewGuid());
        Query("INSERT INTO revora_dis_run_rules(run,uid,difficulty,revision) VALUES(@run,@uid,@difficulty,1)","run",run,"uid",(long)p.Entity.UID,"difficulty",difficulty);
        lock (p.ItemSyncRoot)
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open(); using (var t = c.BeginTransaction()) { Call(DisRanks, null, "Record", c, t, p, run, 1, started); t.Commit(); }
        }
        return run;
    }
    static void RankNpc(GameState p, byte option)
    {
        p.ActiveNpc = 3215; p.Entity.X = p.Map.Npcs[3215].X; p.Entity.Y = p.Map.Npcs[3215].Y;
        Npcs.GetDialog(new NpcRequest(true) { NpcID = 3215, OptionID = option }, p);
    }
    static void VerifyDisRanking(Map town, GameState genuine)
    {
        Check(Scalar("SELECT count(*) FROM revora_dis_finishes WHERE uid=@uid", genuine.Entity.UID) == 1,
            "owned summoned Pluto death records a single durable finish independently of later reward collection");
        foreach (var pair in new[] {
            Tuple.Create(new DateTime(2026, 9, 30, 6, 59, 59, DateTimeKind.Utc), "20260929"),
            Tuple.Create(new DateTime(2026, 9, 30, 7, 0, 0, DateTimeKind.Utc), "20260930"),
            Tuple.Create(new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc), "20261101"),
            Tuple.Create(new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc), "20261101") })
            Check((string)Call(DisRanks, null, "Day", pair.Item1) == pair.Item2,
                "ranking uses Pacific calendar day across midnight and DST at " + pair.Item1.ToString("s"));
        disNow = new DateTime(2026, 9, 29, 20, 0, 0, DateTimeKind.Utc);
        var players = new List<GameState>();
        for (int n = 0; n < 12; n++)
        {
            var p = DisPlayer(town); players.Add(p);
            SeedRankFinish(p, TimeSpan.FromSeconds(n < 2 ? 100 : 100 + n));
        }
        string originalName = players[0].Entity.Name; players[0].Entity.Name = "ChangedName";
        RankNpc(players[0], 60);
        Check(RankClaims(players[0]) == 0 && players[0].Inventory.Count == 0
            && Scalar("SELECT count(*) FROM revora_dis_rank_prizes WHERE uid=@uid", players[0].Entity.UID) == 0,
            "same-day victory cannot claim or prematurely freeze a rank prize");
        disNow = new DateTime(2026, 9, 30, 7, 0, 0, DateTimeKind.Utc);
        Parallel.Invoke(() => RankNpc(players[0], 6), () => RankNpc(players[1], 6));
        Check(players.All(p => !Fenced(p)) && Scalar("SELECT count(*) FROM revora_dis_rank_prizes WHERE day='20260929' AND uid>=@uid", players[0].Entity.UID) == 10
            && Scalar("SELECT rank FROM revora_dis_rank_prizes WHERE uid=@uid", players[0].Entity.UID) == 1
            && Scalar("SELECT rank FROM revora_dis_rank_prizes WHERE uid=@uid", players[1].Entity.UID) == 2
            && Scalar("SELECT rank FROM revora_dis_rank_prizes WHERE uid=@uid", players[9].Entity.UID) == 10,
            "concurrent midnight settlement freezes exactly ten ordered winners with deterministic UID ties");
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open(); using (var q = new Npgsql.NpgsqlCommand("SELECT name FROM revora_dis_rank_prizes WHERE uid=@uid", c))
            { q.Parameters.AddWithValue("uid", (long)players[0].Entity.UID); Check((string)q.ExecuteScalar() == originalName, "ranking retains the recorded finish name instead of a later mutable name"); }
        }
        var filler = new List<ConquerItem>(); while (players[0].Inventory.Count < 40) filler.Add(Pack(players[0], 1088001, 1));
        RankNpc(players[0], 60);
        Check(!Fenced(players[0]) && RankClaims(players[0]) == 0 && players[0].Inventory.Count == 40,
            "full bag preserves the entire first-place rank prize");
        foreach (var item in filler.Take(3)) DisRemove(players[0], item);
        RankNpc(players[0], 60); RankNpc(players[0], 60);
        Check(RankClaims(players[0]) == 1 && players[0].Inventory.Count == 40
            && players[0].Inventory.Objects.Count(i => i.ID == 3008058 && i.Bound) == 3,
            "first-place player receives exactly three bound Sacred packs once");
        var reloaded = players[0].Inventory.Objects.First(i => i.ID == 3008058);
        Check(ConquerItemTable.LoadItem(reloaded.UID).Bound
            && Scalar("SELECT cardinality(rewards) FROM revora_dis_rank_prizes WHERE uid=@uid", players[0].Entity.UID) == 3,
            "rank reward item binding and original UIDs survive the production loader and receipt");
        Query("ALTER TABLE revora_dis_rank_prizes ADD CONSTRAINT reject_rank_claim CHECK(claimed_at IS NULL) NOT VALID");
        RankNpc(players[1], 60);
        Check(Fenced(players[1]) && RankClaims(players[1]) == 0 && players[1].Inventory.Count == 0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid", players[1].Entity.UID) == 0,
            "failed rank receipt rolls back both reward items and leaves the prize claimable");
        Query("ALTER TABLE revora_dis_rank_prizes DROP CONSTRAINT reject_rank_claim"); DemonReset(players[1]); RankNpc(players[1], 6);
        Parallel.For(0, 12, n => Npcs.GetDialog(new NpcRequest(true) { NpcID = 3215, OptionID = 60 }, players[1]));
        Check(!Fenced(players[1]) && RankClaims(players[1]) == 1 && players[1].Inventory.Objects.Count(i => i.ID == 3008058 && i.Bound) == 2,
            "recovered second-place claim grants two packs once under concurrent requests");
        RankNpc(players[2], 60); RankNpc(players[9], 60); RankNpc(players[10], 60);
        Check(RankClaims(players[2]) == 1 && RankClaims(players[9]) == 1 && players[2].Inventory.Count == 1
            && players[9].Inventory.Count == 1 && RankClaims(players[10]) == 0 && players[10].Inventory.Count == 0,
            "third through tenth get one pack and eleventh place cannot claim a rank prize");
        disNow = disNow.AddDays(3); RankNpc(players[3], 60);
        Check(RankClaims(players[3]) == 1 && players[3].Inventory.Count == 1,
            "an unclaimed closed-day prize survives missed days and is recovered without a live settlement timer");
        var foreign = DisPlayer(town); RankNpc(foreign, 60);
        Check(!Fenced(foreign) && foreign.Inventory.Count == 0 && RankClaims(foreign) == 0,
            "ordinary character without an earned finish cannot claim another player's rank reward");

        disNow = new DateTime(2026, 10, 5, 20, 0, 0, DateTimeKind.Utc);
        var delayed = DisPlayer(town); SeedRankFinish(delayed, TimeSpan.FromSeconds(120));
        disNow = disNow.AddDays(4);
        Query(@"CREATE FUNCTION reject_rank_settlement() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture rank settlement'; END $$;
CREATE TRIGGER reject_rank_settlement BEFORE INSERT ON revora_dis_rank_prizes FOR EACH ROW EXECUTE FUNCTION reject_rank_settlement()");
        RankNpc(delayed, 60);
        Check(Fenced(delayed) && Scalar("SELECT count(*) FROM revora_dis_rank_days WHERE day='20261005' AND @uid>0", delayed.Entity.UID) == 0
            && RankClaims(delayed) == 0, "failed settlement cannot leave an empty finalized day or lose a pending winner");
        Query("DROP TRIGGER reject_rank_settlement ON revora_dis_rank_prizes; DROP FUNCTION reject_rank_settlement()"); DemonReset(delayed); RankNpc(delayed, 60);
        Check(!Fenced(delayed) && RankClaims(delayed) == 1 && delayed.Inventory.Count == 3,
            "claim lazily settles an old untouched day and recovers its complete first-place prize after storage repair");
        var forged = DisPlayer(town); bool rejected = false;
        lock (forged.ItemSyncRoot)
        using (var c = DataHolder.MySqlConnection)
        {
            c.Open(); using (var t = c.BeginTransaction())
            { try { Call(DisRanks, null, "Record", c, t, forged, Guid.NewGuid(), 1, disNow.AddSeconds(-1)); } catch (InvalidOperationException) { rejected = true; } }
        }
        Check(rejected && Scalar("SELECT count(*) FROM revora_dis_finishes WHERE uid=@uid", forged.Entity.UID) == 0,
            "finish recorder rejects a missing owned stage-four victory/death source");
        var midnight = DisPlayer(town); disNow = new DateTime(2026, 10, 10, 6, 59, 59, DateTimeKind.Utc);
        DateTime beforeMidnight = disNow, afterMidnight = disNow.AddSeconds(1); int clockCalls = 0;
        Dis.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => ++clockCalls == 1 ? beforeMidnight : afterMidnight));
        try { SeedRankFinish(midnight, TimeSpan.FromSeconds(60)); }
        finally { disNow = afterMidnight; Dis.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => disNow)); }
        Check(Scalar("SELECT count(*) FROM revora_dis_finishes WHERE uid=@uid AND day='20261010' AND elapsed_ticks=610000000", midnight.Entity.UID) == 1,
            "finish waiting across midnight resamples acceptance time under the new day lock");
        VerifyDisSettlementWrites(town);
        VerifyDisPrizeWrites(town);
        long priorClaims=RankClaims(players[0]);
        Query("ALTER TABLE revora_dis_finishes DROP COLUMN difficulty,DROP COLUMN revision;ALTER TABLE revora_dis_rank_prizes DROP COLUMN difficulty CASCADE,DROP COLUMN revision CASCADE");
        Call(DisRanks,null,"Initialize");Call(DisRanks,null,"Initialize");
        Check(RankClaims(players[0])==priorClaims && Scalar("SELECT difficulty+revision FROM revora_dis_rank_prizes WHERE uid=@uid",players[0].Entity.UID)==1
            && ConquerItemTable.LoadItem(reloaded.UID).Bound,"legacy ranking migration preserves claimed reward identities and standard brackets");
        VerifyDisDifficultyRanks(town);
    }
    static void VerifyDisDifficultyRanks(Map town) {
        disNow=disNow.AddDays(3);
        var heroes=new List<GameState>();
        for(int mode=0;mode<3;mode++)for(int n=0;n<11;n++) {
            var hero=DisPlayer(town);heroes.Add(hero);SeedRankFinish(hero,TimeSpan.FromSeconds(50+mode*100+n),mode);
        }
        disNow=disNow.AddDays(1);RankNpc(heroes[0],6);Call(DisRanks,null,"Initialize");
        for(int mode=0;mode<3;mode++) {
            var winner=heroes[mode*11];var last=heroes[mode*11+10];
            Check(Scalar("SELECT rank FROM revora_dis_rank_prizes WHERE uid=@uid",winner.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_dis_rank_prizes WHERE uid=@uid",last.Entity.UID)==0,
                "each difficulty independently admits its top ten despite faster easier finishes");
            RankNpc(winner,60);RankNpc(winner,60);
            Check(!Fenced(winner) && RankClaims(winner)==1 && winner.Inventory.Objects.Count(i=>i.ID==3008058 && i.Bound)==3,
                "each difficulty champion receives one persisted first-place prize after ranking reinitialization");
        }
    }
    static void VerifyDisSettlementWrites(Map town)
    {
        foreach (string fault in new[] { "day", "all-prizes", "second-prize" })
        {
            disNow = disNow.AddDays(2);
            var first = DisPlayer(town); var second = DisPlayer(town);
            SeedRankFinish(first, TimeSpan.FromSeconds(60));
            SeedRankFinish(second, TimeSpan.FromSeconds(90));
            disNow = disNow.AddDays(1);
            string table = fault == "day" ? "revora_dis_rank_days" : "revora_dis_rank_prizes";
            string body = fault == "second-prize" ? "IF NEW.rank=2 THEN RETURN NULL; END IF; RETURN NEW;" : "RETURN NULL;";
            Query("CREATE FUNCTION suppress_dis_settlement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN " + body
                + " END';CREATE TRIGGER suppress_dis_settlement BEFORE INSERT ON " + table
                + " FOR EACH ROW EXECUTE FUNCTION suppress_dis_settlement()");
            try
            {
                RankNpc(first, 60);
                Check(Fenced(first) && Scalar("SELECT count(*) FROM revora_dis_rank_days WHERE day=(SELECT day FROM revora_dis_finishes WHERE uid=@uid)", first.Entity.UID) == 0
                    && Scalar("SELECT count(*) FROM revora_dis_rank_prizes WHERE day=(SELECT day FROM revora_dis_finishes WHERE uid=@uid)", first.Entity.UID) == 0
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid", first.Entity.UID) == 0
                    && first.Inventory.Count == 0,
                    "suppressed " + fault + " write rolls back the entire ranking day and all prizes");
            }
            finally
            {
                Query("DROP TRIGGER suppress_dis_settlement ON " + table + ";DROP FUNCTION suppress_dis_settlement()");
            }
            DemonReset(first);
            Call(DisRanks, null, "Initialize");
            Parallel.Invoke(() => RankNpc(first, 60), () => RankNpc(second, 60));
            RankNpc(first, 60); RankNpc(second, 60);
            Check(!Fenced(first) && !Fenced(second) && RankClaims(first) == 1 && RankClaims(second) == 1
                && first.Inventory.Objects.Count(i => i.ID == 3008058 && i.Bound) == 3
                && second.Inventory.Objects.Count(i => i.ID == 3008058 && i.Bound) == 2,
                "reinitialized ranking recovers both complete prizes once after suppressed " + fault + " write");
        }
    }
    static void VerifyDisPrizeWrites(Map town)
    {
        foreach (string fault in new[] { "first-item", "third-item", "claim" })
        {
            disNow = disNow.AddDays(2);
            var p = DisPlayer(town); SeedRankFinish(p, TimeSpan.FromSeconds(60));
            disNow = disNow.AddDays(1); RankNpc(p, 6);
            string table = fault == "claim" ? "revora_dis_rank_prizes" : "items";
            string verb = fault == "claim" ? "UPDATE" : "INSERT";
            string body = fault == "third-item"
                ? "IF (SELECT count(*) FROM items WHERE entityid=NEW.entityid AND id=3008058)=2 THEN RETURN NULL; END IF; RETURN NEW;"
                : "RETURN NULL;";
            Query("CREATE FUNCTION suppress_dis_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN " + body
                + " END';CREATE TRIGGER suppress_dis_prize BEFORE " + verb + " ON " + table
                + " FOR EACH ROW EXECUTE FUNCTION suppress_dis_prize()");
            try
            {
                RankNpc(p, 60);
                Check(Fenced(p) && RankClaims(p) == 0 && p.Inventory.Count == 0
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid", p.Entity.UID) == 0,
                    "suppressed rank " + fault + " write preserves the whole unclaimed prize without phantom items");
            }
            finally { Query("DROP TRIGGER suppress_dis_prize ON " + table + ";DROP FUNCTION suppress_dis_prize()"); }
            DemonReset(p); Call(DisRanks, null, "Initialize");
            RankNpc(p, 60); RankNpc(p, 60);
            var prizes = p.Inventory.Objects.ToArray();
            Check(!Fenced(p) && RankClaims(p) == 1 && prizes.Length == 3
                && prizes.All(i => i.ID == 3008058 && i.Bound && ConquerItemTable.LoadItem(i.UID).Bound),
                "rank claim recovers three persisted bound packs once after suppressed " + fault + " write");
        }
    }
}
