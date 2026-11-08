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
    static readonly Type Promotions = typeof(Entity).Assembly.GetType("Revora.Game.Features.ClassPromotion");
    static DateTime promotionNow = new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static GameState PromotionPlayer(Map map, byte profession, byte level, uint trainer) {
        var p = StoredProgressionPlayer(map); p.Entity.Class = profession; p.Entity.Level = level;
        p.ActiveNpc = trainer; p.Entity.X = map.Npcs[trainer].X; p.Entity.Y = map.Npcs[trainer].Y; Collection(p);
        p.Entity.MyAchievement = new Achievement(p.Entity); p.Entity.MyAchievement.Load("");
        if (!EntityTable.SaveEntity(p)) throw new Exception("Promotion fixture save failed.");
        return p;
    }
    static void PromotionNpc(GameState p, byte option) {
        Npcs.GetDialog(new NpcRequest(true) { NpcID = p.ActiveNpc, OptionID = option }, p);
    }
    static void PromotionPrepare(GameState p) { PromotionNpc(p, p.Entity.Class == 100 || p.Entity.Class == 101 ? (byte)100 : (byte)1); }
    static long PromotionCount(GameState p) { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='class-promotion-v1'", p.Entity.UID); }
    static void VerifyPromotions(string runtime) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach (string table in new[] { "teamarena", "championarena", "quests", "status", "reward", "rewardclameid", "merchant", "dialyquests", "activeness", "house", "pkexploits", "achievement", "itemadding" })
            Query("CREATE TABLE IF NOT EXISTS " + table + " AS TABLE public." + table + " WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); SpellTable.Load(); RunesSystem.LoadRunesEXP();
        Promotions.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => promotionNow));
        var map = World(runtime, 1004); ConquerItem.ItemUID = new Counter(8100000);
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand(
            "SELECT id,lookface,mapid,cellx,celly FROM public.npcs WHERE id IN (10022,10001,400,4720,4271,4272,5061,10000,51580)", c))
        using (var r = q.ExecuteReader()) while (r.Read()) {
            if (Convert.ToUInt16(r[2]) != 1004) throw new Exception("Installed trainer is outside Job Center.");
            map.AddNpc(new NpcSpawn { UID = Convert.ToUInt32(r[0]), MapID = 1004, X = Convert.ToUInt16(r[3]),
                Y = Convert.ToUInt16(r[4]), Mesh = Convert.ToUInt16(r[1]), Type = Enums.NpcType.Talker });
        }}
        Check(map.Npcs.Count == 9, "all nine installed trainers exist in Job Center; fixtures use their actual coordinates");
        // Independent client destination/receipt table; do not derive expectations from the service.
        int[][] routes = {
            new[]{10022,10,11,12,13,14,15,10819,10820,10821,10822,10823},
            new[]{10001,20,21,22,23,24,25,10813,10814,10815,10816,10817},
            new[]{400,40,41,42,43,44,45,10903,10904,10905,10906,10907},
            new[]{4720,50,51,52,53,54,55,10908,10909,10910,10911,10912},
            new[]{4271,60,61,62,63,64,65,10913,10914,10915,10916,10917},
            new[]{4272,70,71,72,73,74,75,11205,11206,11207,11208,11209},
            new[]{5061,80,81,82,83,84,85,11216,11217,11218,11219,11220},
            new[]{10000,100,101,132,133,134,135,10829,10830,10831,10832,10901},
            new[]{10000,100,101,142,143,144,145,10824,10825,10826,10827,10828},
            new[]{51580,160,161,162,163,164,165,11304,11305,11306,11307,11308}
        };
        int[] levels = { 15, 40, 70, 100, 110 }; GameState last = null;
        foreach (var row in routes) {
            var p = PromotionPlayer(map, (byte)row[1], 14, (uint)row[0]);
            uint wardrobe = StoredQuestGarment(p);
            Check(p.Account.State == AccountTable.AccountState.Player && !p.Fake && p.Entity.Reborn == 0,
                "ordinary non-reborn student starts school " + row[1] + " destination " + row[6]);
            for (int rank = 0; rank < 5; rank++) {
                byte old = p.Entity.Class, target = (byte)row[2 + rank];
                byte confirm = old == 100 ? (byte)254 : old == 101 ? (row[3] == 132 ? (byte)253 : (byte)254) : row[0] == 51580 ? (byte)2 : (byte)3;
                uint type = rank == 1 && row[1] == 40 ? 1072031u : rank == 2 ? 1080001u : rank == 3 ? 1088001u : rank == 4 ? 721090u : 0;
                var material = type == 0 ? null : Pack(p, type, (ushort)(type == 1072031 ? 5 : 1));
                long receipts = PromotionCount(p); int bag = p.Inventory.Count;
                p.Entity.Level = (byte)(levels[rank] - 1); PromotionPrepare(p); PromotionNpc(p, confirm);
                Check(p.Entity.Class == old && PromotionCount(p) == receipts && p.Inventory.Count == bag && !Fenced(p),
                    "rank " + target + " rejects just-below level without spending");
                p.Entity.Level = (byte)levels[rank]; PromotionPrepare(p); PromotionNpc(p, confirm);
                int expectedCount = rank + 1 + (row[1] == 100 ? 1 : 0);
                Check(!Fenced(p) && p.Entity.Class == target && Scalar("SELECT class FROM entities WHERE uid=@uid", p.Entity.UID) == target
                    && PromotionCount(p) == expectedCount && HasAchievement(p, row[7 + rank]), "rank " + target + " atomically promotes and immediately exposes the native achievement");
                if (material != null) Check(!p.Inventory.ContainsUID(material.UID) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND uid=" + material.UID, p.Entity.UID) == 0,
                    "rank " + target + " consumes precisely its offering");
                uint reward = rank == 1 && row[1] == 40 ? 500067u : rank == 3 ? 700031u : rank == 4 ? 1088000u : 0;
                if (reward != 0) Check(p.Inventory.Objects.Count(i => i.ID == reward) == 1, "rank " + target + " retains the existing reward");
                bag = p.Inventory.Count; Parallel.For(0, 6, i => PromotionNpc(p, confirm));
                Check(p.Entity.Class == target && PromotionCount(p) == expectedCount && p.Inventory.Count == bag,
                    "rank " + target + " concurrent confirmation replays cannot advance again");
            }
            PromotionPrepare(p); PromotionNpc(p, row[0] == 51580 ? (byte)2 : (byte)3);
            Check(p.Entity.Class == row[6] && !Fenced(p), "highest rank does not overflow school " + row[1]); last = p;
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1", wardrobe)==1,
                "five promotions preserve stored wardrobe design for school destination " + row[6]);
        }
        Check(Scalar("SELECT count(DISTINCT achievement_id) FROM revora_achievement_evidence WHERE source='class-promotion-v1' AND uid<>@uid", 0) == 50,
            "all fifty installed promotion achievement identities have a real trainer earning route");
        uint lastWardrobe=last.Entity.StorageItems.Keys.Single();
        var reloaded = ReloadGold(last); ConquerItemTable.LoadItems(reloaded); new TitleStorage().Login(reloaded);
        Check(reloaded.Entity.Class == 165 && reloaded.Entity.Level == 110 && reloaded.Inventory.Objects.Any(i => i.ID == 1088000)
            && HasAchievement(reloaded, 11308) && PromotionCount(reloaded) == 5, "full character and inventory reload retains promotion, reward and receipt");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",lastWardrobe)==1,"production promotion reload preserves stored wardrobe design outside the bag");
        EntityTable.SaveEntity(reloaded); PromotionNpc(reloaded, 2);
        Check(PromotionCount(reloaded) == 5 && reloaded.Entity.Class == 165, "save and reconnect replay retain one receipt per earned rank");
        VerifyPromotionFailures(map);
        VerifyEarnedPromotionOffering(runtime, map);
    }
    static void VerifyEarnedPromotionOffering(string runtime, Map jobCenter) {
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id IN (600050,600003) OR (id BETWEEN 600005 AND 600017 AND id%2=1) OR id BETWEEN 600018 AND 600042");
        for (ushort id = 1042; id <= 1050; id++) World(runtime, id);
        Moon.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => max == 7 ? moonRoute : max == 25 ? 0 : moonLoot));
        Call(Moon, null, "Initialize"); Call(Moon, null, "PlaceNpcs");
        foreach (byte option in new byte[] { 1, 2 }) {
            var p = PromotionPlayer(jobCenter, option == 1 ? (byte)14 : (byte)13, 110, 10022);
            uint wardrobe=StoredQuestGarment(p);
            p.Entity.Teleport(1002, 489, 622); MoonToLife(p); MoonNpc(p, 600018, option);
            uint materialType = option == 1 ? 721090u : 1088001u; int earned = option == 1 ? 1 : 3;
            Check(!Fenced(p) && p.Entity.MapID == 1002 && p.Inventory.Objects.Count(i => i.ID == materialType) == earned
                && Scalar("SELECT count(*) FROM revora_moon_results WHERE uid=@uid", p.Entity.UID) == 1,
                "ordinary six-tactic quest completion earns actual promotion material " + materialType);
            var material = p.Inventory.Objects.First(i => i.ID == materialType); bool bound = material.Bound;
            p.Entity.Teleport(1004, jobCenter.Npcs[10022].X, jobCenter.Npcs[10022].Y); p.ActiveNpc = 10022;
            PromotionPrepare(p); PromotionNpc(p, 3);
            Check(!Fenced(p) && p.Entity.Class == (option == 1 ? 15 : 14) && PromotionCount(p) == 1
                && p.Inventory.Objects.Count(i => i.ID == materialType) == earned - 1
                && p.Inventory.Objects.Single(i => i.ID == (option == 1 ? 1088000u : 700031u)).Bound == bound
                && HasAchievement(p, option == 1 ? 10823 : 10822),
                "earned quest offering reaches trainer reward and promotion achievement without injected materials " + materialType);
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Moon Box quest and promotion preserve stored wardrobe design");
        }
    }
    static void VerifyPromotionFailures(Map map) {
        foreach (string mode in new[] { "no-offer", "expired", "cancelled", "wrong-request", "far", "wrong-school", "trade", "dead", "fake", "replaced", "wrong-map", "unloaded", "missing-material", "locked", "suspicious" }) {
            var p = PromotionPlayer(map, 14, 110, 10022); var material = Pack(p, 721090, 1);
            if (mode != "no-offer") PromotionPrepare(p);
            if (mode == "expired") promotionNow = promotionNow.AddMinutes(2);
            if (mode == "cancelled") Npcs.GetDialog(new NpcRequest(true) { NpcID = 10022, OptionID = 255 }, p);
            if (mode == "wrong-request") Npcs.GetDialog(new NpcRequest(true) { NpcID = 400, OptionID = 3 }, p);
            if (mode == "far") p.Entity.X += 20;
            if (mode == "wrong-school") p.ActiveNpc = 400;
            if (mode == "trade") p.Trade.InTrade = true;
            if (mode == "dead") p.Entity.Hitpoints = 0;
            if (mode == "fake") p.Fake = true;
            if (mode == "replaced") { GameState removed; Kernel.GamePool.TryRemove(p.Entity.UID, out removed); }
            if (mode == "wrong-map") p.Entity.MapID = 1002;
            if (mode == "unloaded") p.Entity.FullyLoaded = false;
            if (mode == "missing-material") DisRemove(p, material);
            if (mode == "locked") material.Lock = 1;
            if (mode == "suspicious") material.Suspicious = true;
            PromotionNpc(p, 3);
            Check(p.Entity.Class == 14 && PromotionCount(p) == 0 && (mode == "missing-material" || p.Inventory.ContainsUID(material.UID)),
                "invalid promotion rejects without spending: " + mode);
        }
        var changed = PromotionPlayer(map, 10, 140, 10022); PromotionPrepare(changed); changed.Entity.Class = 11; PromotionNpc(changed, 3);
        Check(changed.Entity.Class == 11 && PromotionCount(changed) == 0, "direct class setter and outdated offer cannot manufacture promotion receipts");
        var bound = PromotionPlayer(map, 13, 100, 10022); var meteor = Pack(bound, 1088001, 2); meteor.Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)meteor.UID); PromotionPrepare(bound); PromotionNpc(bound, 3);
        Check(bound.Entity.Class == 14 && meteor.StackSize == 1 && bound.Inventory.Objects.Single(i => i.ID == 700031).Bound
            && Scalar("SELECT stacksize FROM items WHERE entityid=@uid AND uid=" + meteor.UID, bound.Entity.UID) == 1, "stack debit preserves its remainder and bound offering cannot create tradable reward");
        var full = PromotionPlayer(map, 14, 110, 10022); var box = Pack(full, 721090, 2);
        for (int i = 0; i < 39; i++) Pack(full, 1088001, 1);
        PromotionPrepare(full); PromotionNpc(full, 3);
        Check(full.Entity.Class == 14 && box.StackSize == 2 && full.Inventory.Count == 40 && PromotionCount(full) == 0, "full bag with partial material stack preserves everything");
        DisRemove(full, box); box = Pack(full, 721090, 1); PromotionPrepare(full); PromotionNpc(full, 3);
        Check(full.Entity.Class == 15 && full.Inventory.Count == 40 && !full.Inventory.ContainsUID(box.UID), "fully consumed material frees the reward slot in a full bag");
        var ores = PromotionPlayer(map, 41, 40, 400); var a = Pack(ores, 1072031, 3); var b = Pack(ores, 1072031, 4);
        PromotionPrepare(ores); PromotionNpc(ores, 3);
        Check(ores.Entity.Class == 42 && !ores.Inventory.ContainsUID(a.UID) && b.StackSize == 2, "archer takes five ores across stacks and preserves the remainder");
        var legacy = PromotionPlayer(map, 14, 110, 10022); var oldBox = Pack(legacy, 721020, 0); PromotionPrepare(legacy); PromotionNpc(legacy, 3);
        Check(legacy.Entity.Class == 15 && !legacy.Inventory.ContainsUID(oldBox.UID), "legacy zero-stack singleton Moon Box remains a valid offering");
        foreach (string mode in new[] { "profile", "owner", "bound", "stack", "lock", "inventory" }) {
            var p = PromotionPlayer(map, 14, 110, 10022); var material = Pack(p, 721090, 1); PromotionPrepare(p);
            if (mode == "profile") Query("UPDATE entities SET class=24 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            else if (mode == "inventory") Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@item,1088001,@uid,1)", "item", (long)ConquerItem.ItemUID.Next, "uid", (long)p.Entity.UID);
            else Query("UPDATE items SET " + (mode == "owner" ? "entityid=1" : mode == "bound" ? "bound=1" : mode == "stack" ? "stacksize=2" : "locked=1") + " WHERE uid=@uid", "uid", (long)material.UID);
            PromotionNpc(p, 3);
            Check(Fenced(p) && p.Entity.Class == 14 && PromotionCount(p) == 0 && p.Inventory.ContainsUID(material.UID)
                && Scalar("SELECT class FROM entities WHERE uid=@uid", p.Entity.UID) == (mode == "profile" ? 24 : 14), "stale " + mode + " aborts the entire promotion and fences save-overwrite");
        }
        foreach (string table in new[] { "entities", "items", "revora_achievement_evidence" }) {
            var p = PromotionPlayer(map, 14, 110, 10022); var material = Pack(p, 721090, 1); PromotionPrepare(p);
            string action = table == "entities" ? "UPDATE" : "INSERT";
            Query("CREATE FUNCTION suppress_promotion() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_promotion BEFORE " + action + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_promotion()");
            try { PromotionNpc(p, 3); } finally { Query("DROP TRIGGER suppress_promotion ON " + table + ";DROP FUNCTION suppress_promotion()"); }
            Check(Fenced(p) && p.Entity.Class == 14 && PromotionCount(p) == 0 && p.Inventory.ContainsUID(material.UID)
                && Scalar("SELECT class FROM entities WHERE uid=@uid", p.Entity.UID) == 14
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid", p.Entity.UID) == 1, "suppressed " + table + " write rolls back class, debit, reward and achievement");
            DemonReset(p); PromotionPrepare(p); PromotionNpc(p, 3);
            Check(!Fenced(p) && p.Entity.Class == 15 && PromotionCount(p) == 1, "retry after repaired " + table + " commits once");
        }
        var second = PromotionPlayer(map, 14, 110, 10022); second.Entity.Reborn = 2; Pack(second, 721090, 1); PromotionPrepare(second); PromotionNpc(second, 3);
        Check(second.Entity.Class == 15 && PromotionCount(second) == 1, "second-reborn ordinary player can earn a new promotion receipt");
        second.Entity.Class = 14; Pack(second, 721090, 1); PromotionPrepare(second); PromotionNpc(second, 3);
        Check(second.Entity.Class == 15 && PromotionCount(second) == 1 && second.Inventory.Objects.Count(i => i.ID == 1088000) == 2,
            "later-life promotion can repeat its service while lifetime achievement remains unique");
    }
}
