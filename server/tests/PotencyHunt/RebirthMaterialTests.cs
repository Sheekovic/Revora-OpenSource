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
    static readonly Type WaterLoot = typeof(Entity).Assembly.GetType("Revora.Game.Features.CleanWaterDrops");
    static readonly Type StoneCraft = typeof(Entity).Assembly.GetType("Revora.Game.Features.HeavenlyStoneCrafting");
    static DateTime stoneNow = new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static int waterRoll;
    static GameState StonePlayer(Map town) {
        var p = StoredProgressionPlayer(town); p.Entity.X = town.Npcs[59558].X; p.Entity.Y = town.Npcs[59558].Y;
        p.ActiveNpc = 59558; Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity); p.Entity.MyAchievement.Load("");
        if (!EntityTable.SaveEntity(p)) throw new Exception("Stone fixture save failed."); return p;
    }
    static void StoneNpc(GameState p, byte option) { Npcs.GetDialog(new NpcRequest(true) { NpcID = 59558, OptionID = option }, p); }
    static long Stones(GameState p) { return Scalar("SELECT count(*) FROM revora_heavenly_stone_crafts WHERE uid=@uid", p.Entity.UID); }
    static long Waters(GameState p) { return Scalar("SELECT count(*) FROM revora_clean_water_pickups WHERE collector=@uid", p.Entity.UID); }
    static ConquerItem[] StoneMaterials(GameState p, ushort stack, bool water = true) {
        var items = Enumerable.Range(0, 7).Select(i => Pack(p, 700001u + (uint)i * 10, stack)).ToList();
        if (water) items.Add(Pack(p, 721258, stack)); return items.ToArray();
    }
    static Entity WaterMonster(GameState p) {
        var mob = LootMonster(p); var info = MonsterInformation.MonsterInformations[8500].Copy();
        mob.MonsterInfo = info; info.Owner = mob; mob.Name = info.Name; return mob;
    }
    static FloorItem WaterDrop(GameState p) {
        var before = p.Map.FloorItems.Keys.ToArray(); Call(WaterLoot, null, "TryDrop", WaterMonster(p), p.Entity);
        return p.Map.FloorItems.Values.Single(i => !before.Contains(i.UID));
    }
    static GameState VerifyRebirthMaterials(string runtime, Map town) {
        // The starter suite begins with the full character schema before mentor FKs are installed.
        if(!fullStarterSchema) Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach (string table in new[] { "teamarena", "championarena", "quests", "status", "reward", "rewardclameid", "merchant", "dialyquests", "activeness", "house", "pkexploits", "achievement", "itemadding" })
            if(fullStarterSchema && table=="championarena")
                Query("CREATE TABLE IF NOT EXISTS championarena (LIKE public.championarena INCLUDING DEFAULTS)");
            else Query("CREATE TABLE IF NOT EXISTS " + table + " AS TABLE public." + table + " WITH NO DATA");
        if(fullStarterSchema)Query("ALTER TABLE championarena ADD PRIMARY KEY(entityid);ALTER TABLE championarena ADD COLUMN IF NOT EXISTS revora_revision bigint NOT NULL DEFAULT 0");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); Call(WaterLoot, null, "Initialize"); Call(StoneCraft, null, "Initialize");
        Call(WaterLoot, null, "Initialize"); Call(StoneCraft, null, "Initialize");
        StoneCraft.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => stoneNow));
        WaterLoot.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => waterRoll)); Config.CleanWaterBasisPoints = 2500;
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand("SELECT id,mapid,cellx,celly,lookface FROM public.npcs WHERE id=59558", c))
        using (var r = q.ExecuteReader()) { if (!r.Read() || Convert.ToUInt16(r[1]) != 1002) throw new Exception("Sereth placement missing.");
            town.AddNpc(new NpcSpawn { UID = 59558, MapID = 1002, X = Convert.ToUInt16(r[2]), Y = Convert.ToUInt16(r[3]), Mesh = Convert.ToUInt16(r[4]), Type = Enums.NpcType.Talker }); } }
        Check(CtfCount("SELECT count(*) FROM public.monsterspawns WHERE mapid=1212 AND npctype=8500") > 0
            && MonsterInformation.MonsterInformations[8500].Name == "Waterlord", "installed Waterlord profile and Adventure Islands spawn support the restored source");
        var island = World(runtime, 1212); ConquerItem.ItemUID = new Counter(8300000);
        var p = StonePlayer(town); p.Entity.Teleport(1212, 517, 732); var mob = WaterMonster(p);
        mob.Hitpoints = mob.MaxHitpoints = 100; mob.IsDropped = false; mob.Die(p.Entity);
        var drop = island.FloorItems.Values.Single(i => i.ItemID == 721258);
        Check(p.Account.State == AccountTable.AccountState.Player && !p.Fake && !drop.Item.Bound && drop.OwnerUID == p.Entity.UID,
            "actual installed-family monster death produces an owned unbound Clean Water floor item");
        int before = island.FloorItems.Count; Parallel.For(0, 8, i => Call(WaterLoot, null, "TryDrop", mob, p.Entity));
        Check(island.FloorItems.Count == before, "concurrent duplicate death notifications cannot repeat a Clean Water roll");
        foreach (string invalid in new[] { "short", "length", "family", "seal", "oversize" }) {
            var packet = LootPacket(drop); if (invalid == "short") Array.Resize(ref packet, 16); else if (invalid == "length") packet[0]--;
            else if (invalid == "family") packet[2]++; else if (invalid == "seal") packet[packet.Length - 1] ^= 1; else Array.Resize(ref packet, 129);
            p.Entity.X = drop.X; p.Entity.Y = drop.Y; FloorItem.Process(packet, p);
            Check(!drop.PickedUpAlready && Waters(p) == 0, "malformed Clean Water pickup rejected: " + invalid);
        }
        var stranger = StonePlayer(town); stranger.Entity.Teleport(1212, drop.X, drop.Y); Loot(stranger, drop);
        Check(stranger.Inventory.Count == 0 && !drop.PickedUpAlready, "fresh Clean Water drop protects its actual killer's ownership");
        Loot(p, drop);
        Check(!Fenced(p) && drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID) && Waters(p) == 1
            && ConquerItemTable.LoadItem(drop.Item.UID).ID == 721258, "native floor pickup commits Clean Water and its origin receipt together");
        p.Entity.Teleport(1002, town.Npcs[59558].X, town.Npcs[59558].Y); p.ActiveNpc = 59558; StoneMaterials(p, 1, false);
        StoneNpc(p, 38); Parallel.For(0, 10, i => StoneNpc(p, 39));
        Check(!Fenced(p) && Stones(p) == 1 && p.Inventory.Count == 1 && p.Inventory.Objects[0].ID == 721259 && !p.Inventory.Objects[0].Bound,
            "hunted Clean Water and seven normal gems craft exactly one Heavenly Stone through Sereth");
        Check(Scalar("SELECT count(*) FROM revora_heavenly_stone_materials m JOIN revora_heavenly_stone_crafts c ON c.id=m.craft WHERE c.uid=@uid", p.Entity.UID) == 8
            && p.Entity.Reborn == 0 && !HasAchievement(p, 11030) && !HasAchievement(p, 10315), "eight material identities are recorded without falsely claiming rebirth completion");
        var loaded = ReloadGold(p); ConquerItemTable.LoadItems(loaded); new TitleStorage().Login(loaded);
        Check(loaded.Inventory.Count == 1 && loaded.Inventory.Objects[0].ID == 721259 && Stones(loaded) == 1 && Waters(loaded) == 1,
            "production character/item reload preserves earned water provenance and the crafted stone");
        loaded.ActiveNpc = 59558; StoneNpc(loaded, 39); Check(Stones(loaded) == 1 && loaded.Inventory.Count == 1, "new session cannot replay the old crafting offer");
        VerifyStoneCraftFailures(town); VerifyWaterFailures(town, island); return loaded;
    }
    static void VerifyStoneCraftFailures(Map town) {
        foreach (string mode in new[] { "missing", "wrong-grade", "locked", "suspicious", "no-offer", "expired", "cancel", "changed-stack", "changed-bind", "far", "trade", "dead", "replaced", "fake", "unloaded" }) {
            var p = StonePlayer(town); var items = StoneMaterials(p, 2);
            if (mode == "missing") DisRemove(p, items[3]);
            if (mode == "wrong-grade") items[3].ID++;
            if (mode == "locked") items[3].Lock = 1;
            if (mode == "suspicious") items[3].Suspicious = true;
            if (mode != "no-offer") StoneNpc(p, 38);
            if (mode == "expired") stoneNow = stoneNow.AddMinutes(2);
            if (mode == "cancel") StoneNpc(p, 255);
            if (mode == "changed-stack") items[0].StackSize++;
            if (mode == "changed-bind") items[0].Bound = true;
            if (mode == "far") p.Entity.X += 20;
            if (mode == "trade") p.Trade.InTrade = true;
            if (mode == "dead") p.Entity.Hitpoints = 0;
            if (mode == "replaced") Kernel.GamePool.Remove(p.Entity.UID);
            if (mode == "fake") p.Fake = true;
            if (mode == "unloaded") p.Entity.FullyLoaded = false;
            StoneNpc(p, 39); Check(Stones(p) == 0 && p.Inventory.Count == (mode == "missing" ? 7 : 8), "ineligible stone request preserves its materials: " + mode);
        }
        var bound = StonePlayer(town); var material = StoneMaterials(bound, 3); material[0].Bound = true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)material[0].UID); StoneNpc(bound, 38); StoneNpc(bound, 39);
        Check(!Fenced(bound) && bound.Inventory.Objects.Single(i => i.ID == 721259).Bound && material.All(i => i.StackSize == 2)
            && Scalar("SELECT sum(stacksize) FROM items WHERE entityid=@uid AND id<>721259", bound.Entity.UID) == 16,
            "one of each stacked material is consumed and any bound input binds the Heavenly Stone");
        var full = StonePlayer(town); var stacked = StoneMaterials(full, 2); for (int i = 0; i < 32; i++) Pack(full, 1088001, 1);
        StoneNpc(full, 38); StoneNpc(full, 39); Check(Stones(full) == 0 && stacked.All(i => i.StackSize == 2), "full bag cannot consume partial stacks without room for its result");
        DisRemove(full, stacked[7]); Pack(full, 721258, 0); StoneNpc(full, 38); StoneNpc(full, 39);
        Check(!Fenced(full) && Stones(full) == 1 && full.Inventory.Count == 40, "legacy singleton offering frees a reward slot in a full bag");
        foreach (string mode in new[] { "owner", "stack", "bound", "lock", "map", "items", "revora_heavenly_stone_crafts", "revora_heavenly_stone_materials" }) {
            var p = StonePlayer(town); var items = StoneMaterials(p, 1); StoneNpc(p, 38); bool trigger = mode == "items" || mode.StartsWith("revora_");
            if (mode == "map") Query("UPDATE entities SET mapid=1004 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            else if (!trigger) Query("UPDATE items SET " + (mode == "owner" ? "entityid=1" : mode == "stack" ? "stacksize=2" : mode == "bound" ? "bound=1" : "locked=1") + " WHERE uid=@uid", "uid", (long)items[7].UID);
            else Query("CREATE FUNCTION suppress_stone() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_stone BEFORE INSERT ON " + mode + " FOR EACH ROW EXECUTE FUNCTION suppress_stone()");
            try { StoneNpc(p, 39); } finally { if (trigger) Query("DROP TRIGGER suppress_stone ON " + mode + ";DROP FUNCTION suppress_stone()"); }
            Check(Fenced(p) && p.Inventory.Count == 8 && Stones(p) == 0 && items.All(i => ConquerItemTable.LoadItem(i.UID) != null), "failed stone " + mode + " rolls back every consumed item and receipt");
            if (trigger) { DemonReset(p); StoneNpc(p, 38); StoneNpc(p, 39); Check(!Fenced(p) && Stones(p) == 1, "repaired stone " + mode + " can retry once"); }
        }
    }
    static void VerifyWaterFailures(Map town, Map island) {
        var p = StonePlayer(town); p.Entity.Teleport(1212, 517, 732);
        foreach (int roll in new[] { 2499, 2500, 9999 }) {
            waterRoll = roll; int before = island.FloorItems.Count; Call(WaterLoot, null, "TryDrop", WaterMonster(p), p.Entity);
            Check(island.FloorItems.Count == before + (roll < 2500 ? 1 : 0), "configured 25-percent Clean Water boundary at roll " + roll);
        }
        waterRoll = 0;
        foreach (string mode in new[] { "family", "living", "companion", "guard", "reviver", "foreign-owner", "noncanonical", "fake-killer", "foreign-map" }) {
            var mob = WaterMonster(p); int before = island.FloorItems.Count;
            if (mode == "family") mob.MonsterInfo.ID = 8419; if (mode == "living") mob.Hitpoints = 1; if (mode == "companion") mob.Companion = true;
            if (mode == "guard") mob.MonsterInfo.Guard = true; if (mode == "reviver") mob.MonsterInfo.Reviver = true;
            if (mode == "foreign-owner") mob.MonsterInfo.Owner = WaterMonster(p); if (mode == "noncanonical") island.Entities.Remove(mob.UID);
            if (mode == "fake-killer") p.Fake = true; if (mode == "foreign-map") mob.MapID = 1002;
            Call(WaterLoot, null, "TryDrop", mob, p.Entity); p.Fake = false;
            Check(island.FloorItems.Count == before, "invalid water source is excluded: " + mode);
        }
        var failedLife = WaterMonster(p); int count = island.FloorItems.Count; waterRoll = 2500;
        Call(WaterLoot, null, "TryDrop", failedLife, p.Entity); waterRoll = 0; Call(WaterLoot, null, "TryDrop", failedLife, p.Entity);
        Check(island.FloorItems.Count == count, "a failed Clean Water roll cannot be retried for the same corpse");
        failedLife.DeathStamp = failedLife.DeathStamp.AddSeconds(1); Call(WaterLoot, null, "TryDrop", failedLife, p.Entity);
        Call(WaterLoot, null, "TryDrop", failedLife, p.Entity);
        Check(island.FloorItems.Count == count + 1, "a later respawn life gets exactly one new Clean Water roll");
        foreach (int chance in new[] { 0, -1 }) {
            Config.CleanWaterBasisPoints = chance; count = island.FloorItems.Count; Call(WaterLoot, null, "TryDrop", WaterMonster(p), p.Entity);
            Check(island.FloorItems.Count == count, "nonpositive configuration disables Clean Water drops: " + chance);
        }
        Config.CleanWaterBasisPoints = 2500;
        var direct = WaterDrop(p); p.Entity.X = direct.X; p.Entity.Y = direct.Y; p.Screen.Add(direct); long receipts = Waters(p);
        ItemHandler.PickupItem(new FloorItem(true) { UID = direct.UID }, p);
        Check(!Fenced(p) && direct.PickedUpAlready && Waters(p) == receipts + 1, "auto-hunt/direct pickup dispatch shares the owned Clean Water transaction");
        var concurrent = WaterDrop(p); p.Entity.X = concurrent.X; p.Entity.Y = concurrent.Y; receipts = Waters(p);
        Parallel.For(0, 12, i => FloorItem.Process(LootPacket(concurrent), p));
        Check(!Fenced(p) && concurrent.PickedUpAlready && Waters(p) == receipts + 1, "parallel native pickups deliver one Clean Water and one receipt");
        var rival = StonePlayer(town); rival.Entity.Teleport(1212, p.Entity.X, p.Entity.Y); var contest = WaterDrop(p);
        contest.OnFloor = Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter); receipts = Waters(p) + Waters(rival);
        Task.WaitAll(Task.Run(() => Loot(p, contest)), Task.Run(() => Loot(rival, contest)));
        Check(!Fenced(p) && !Fenced(rival) && contest.PickedUpAlready && Waters(p) + Waters(rival) == receipts + 1,
            "two eligible collectors race for a released drop without duplicating its item");
        var full = StonePlayer(town); full.Entity.Teleport(1212, 517, 732); for (int i = 0; i < 40; i++) Pack(full, 1088001, 1);
        var waiting = WaterDrop(full); Loot(full, waiting);
        Check(!Fenced(full) && !waiting.PickedUpAlready && full.Inventory.Count == 40 && Waters(full) == 0, "full bag preserves Clean Water on the ground");
        var expired = WaterDrop(p); expired.OnFloor = Time32.Now.AddSeconds(-Constants.FloorItemSeconds); Loot(p, expired);
        Check(!expired.PickedUpAlready && !p.Inventory.ContainsUID(expired.Item.UID), "expired Clean Water cannot be recovered through a stale request");
        var remote = WaterDrop(p); p.Entity.X = (ushort)(remote.X + 1); p.Entity.Y = remote.Y; FloorItem.Process(LootPacket(remote), p);
        Check(!remote.PickedUpAlready, "client-authored coordinates cannot replace the real collector position");
        var removed = WaterDrop(p); island.RemoveFloorItem(removed); Loot(p, removed);
        Check(!removed.PickedUpAlready && !p.Inventory.ContainsUID(removed.Item.UID), "removed Clean Water cannot be collected from an old reference");
        var replacement = new FloorItem(true) { UID = removed.UID, MapID = island.ID, X = removed.X, Y = removed.Y, Type = FloorItem.Drop };
        island.AddFloorItem(replacement); island.RemoveFloorItem(removed);
        Check(ReferenceEquals(island.FloorItems[replacement.UID], replacement), "stale Clean Water expiry cannot remove a replacement floor object"); island.RemoveFloorItem(replacement);
        var altered = WaterDrop(p); altered.Item.Bound = true; Loot(p, altered);
        Check(Fenced(p) && !altered.PickedUpAlready && !p.Inventory.ContainsUID(altered.Item.UID), "tampered floor binding cannot bypass the saved Clean Water origin");
        DemonReset(p); altered.Item.Bound = false; Loot(p, altered);
        Check(!Fenced(p) && altered.PickedUpAlready, "restored floor origin can retry through the same transaction");
        foreach (string table in new[] { "items", "revora_clean_water_pickups" }) {
            var drop = WaterDrop(p); long before = Waters(p);
            Query("CREATE FUNCTION suppress_water() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_water BEFORE INSERT ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_water()");
            try { Loot(p, drop); } finally { Query("DROP TRIGGER suppress_water ON " + table + ";DROP FUNCTION suppress_water()"); }
            Check(Fenced(p) && !drop.PickedUpAlready && island.FloorItems.ContainsKey(drop.UID) && Waters(p) == before && !p.Inventory.ContainsUID(drop.Item.UID),
                "failed water " + table + " keeps the floor item available and rolls back delivery");
            DemonReset(p); Loot(p, drop); Check(!Fenced(p) && drop.PickedUpAlready && Waters(p) == before + 1, "repaired water " + table + " retries without duplication");
        }
    }
}
