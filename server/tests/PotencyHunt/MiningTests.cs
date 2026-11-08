using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type MiningService = typeof(Entity).Assembly.GetType("Revora.Game.Features.MiningRewards");
    static readonly Type MinePassage = typeof(Entity).Assembly.GetType("Revora.Game.Features.MiningPassage");
    static DateTime miningNow = new DateTime(2032, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    static int miningGem;
    static byte[] MiningPacket(GameState p) {
        var bytes = new Data(true) { ID = Data.SwingPickaxe, UID = p.Entity.UID }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); return bytes;
    }
    static void StartMining(GameState p) { Data.Process(MiningPacket(p), p); }
    static void MineTick(GameState p) { Revora.Game.ConquerStructures.Mining.Mine(p); }
    static void MineTravel(GameState p) { Npcs.GetDialog(new NpcRequest(true) { NpcID = 10080, OptionID = 5 }, p); }
    static void MinePortal(GameState p, ushort destination) {
        p.Map.Portals.Clear(); Call(typeof(Map), p.Map, "LoadPortals");
        var portal = p.Map.Portals.First(i => i.DestinationMapID == destination);
        p.Entity.X = portal.CurrentX; p.Entity.Y = portal.CurrentY;
        var bytes = new Data(true) { ID = Data.UsePortal, UID = p.Entity.UID, dwParam = (uint)(portal.CurrentX | portal.CurrentY << 16) }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); Data.Process(bytes, p);
        Check(!Fenced(p) && p.Entity.MapID == destination, "existing native portal handler follows the installed route to " + destination);
    }
    static long MineCount(GameState p) { return Scalar("SELECT COALESCE(sum(yields),0) FROM revora_mining_state WHERE uid=@uid", p.Entity.UID); }
    static GameState Miner(Map mine, uint type = 562000) {
        var p = AppearancePlayer(mine); p.Entity.X = 155; p.Entity.Y = 95;
        Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity); p.Entity.MyAchievement.Load("");
        var item = Pack(p, type, 1); item.Durability = item.MaximDurability = ConquerItemInformation.BaseInformations[type].Durability;
        Query("UPDATE items SET durability=@dur,maximdurability=@dur WHERE uid=@uid", "dur", (int)item.Durability, "uid", (long)item.UID);
        ItemHandler.EquipItem(new ItemUsage(true) { UID = item.UID, dwParam = 4, Action = ItemUsage.Mode.EquipItem }, p);
        if (p.Equipment.TryGetItem(4) != item || SavedRune(item, "position") != 4) throw new Exception("Mining tool equip failed.");
        return p;
    }
    static void MiningNpc(Map map, uint id) {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=@id", c)) {
            q.Parameters.AddWithValue("id", (long)id);
            using (var r = q.ExecuteReader()) { if (!r.Read() || Convert.ToUInt16(r[0]) != map.ID) throw new Exception("Mining NPC placement missing: " + id);
                map.AddNpc(new NpcSpawn { UID = id, MapID = map.ID, X = Convert.ToUInt16(r[1]), Y = Convert.ToUInt16(r[2]), Mesh = Convert.ToUInt16(r[3]), Type = Enums.NpcType.Talker }); }
        }}
    }
    static void VerifyMining(string runtime, Map town, bool adventure = false) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach (string table in new[] { "teamarena", "championarena", "quests", "status", "reward", "rewardclameid", "merchant", "dialyquests", "activeness", "house", "pkexploits", "achievement", "itemadding" })
            Query("CREATE TABLE IF NOT EXISTS " + table + " AS TABLE public." + table + " WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); Call(MiningService, null, "Initialize"); Call(MiningService, null, "Initialize");
        Call(MinePassage, null, "Initialize"); Call(MinePassage, null, "Initialize");
        Call(BookShop, null, "Initialize"); Call(WaterLoot, null, "Initialize"); Call(StoneCraft, null, "Initialize"); ShopFile.Load();
        if (adventure) { Query("CREATE TABLE profs AS TABLE public.profs WITH NO DATA"); Call(FirstRite, null, "Initialize"); FirstRite.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => 0)); DataHolder.ReadStats(); SpellTable.Load(); RunesSystem.LoadRunesEXP(); }
        MiningService.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => miningNow));
        MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => max == 10000 ? 0 : max == 100 ? 99 : max == 3 || max == 4 ? miningGem : 0));
        BookShop.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => miningNow));
        StoneCraft.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => miningNow));
        WaterLoot.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => 0));
        Config.MiningNormalGemBasisPoints = 100; Config.CleanWaterBasisPoints = 2500;
        ConquerItem.ItemUID = new Counter(8500000);
        var plain = World(runtime, 1028); var forest = World(runtime, 1025); var canyon = World(runtime, 1026);
        MiningNpc(town, 1); MiningNpc(town, 59558); MiningNpc(town, 10080);
        var phoenix = World(runtime, 1011); var ape = World(runtime, 1020); MiningNpc(phoenix, 125); MiningNpc(ape, 126);
        var p = StonePlayer(town); uint wardrobe=StoredQuestGarment(p); p.Entity.X = town.Npcs[1].X; p.Entity.Y = town.Npcs[1].Y;
        if (adventure) { p.Entity.X -= 2; p.Entity.Y -= 2; p.Entity.Class = 15; p.Entity.Body = 1003;
            if (!EntityTable.SaveEntity(p)) throw new Exception("Adventure starting class save failed.");
            Check(p.Map.Floor[p.Entity.X, p.Entity.Y, MapObjectType.Player], "complete journey starts on a legal tile beside the installed Storekeeper"); }
        p.Entity.AdjustMoney(2200); ItemHandler.HandleBuyFromNPC(BuyBook(562000, 1, 1), p);
        var pick = p.Inventory.Objects.Single(i => i.ID == 562000);
        Check(!Fenced(p) && p.Account.State == AccountTable.AccountState.Player && p.Entity.Money == 1200 && BookPurchases(p) == 1
            && Scalar("SELECT money FROM entities WHERE uid=@uid", p.Entity.UID) == 1200 && pick.Durability == 1099,
            "ordinary installed store buys a durable Pickaxe for 1,000 silver, including pending earned silver");
        Parallel.For(0, 8, i => ItemHandler.HandleBuyFromNPC(BuyBook(562000, 1, 1), p));
        Check(p.Inventory.Count == 1 && BookPurchases(p) == 1 && p.Entity.Money == 1200, "repeated purchase cannot duplicate or overdraw the Pickaxe");
        ItemHandler.EquipItem(new ItemUsage(true) { UID = pick.UID, dwParam = 4, Action = ItemUsage.Mode.EquipItem }, p);
        Check(p.Equipment.TryGetItem(4) == pick && SavedRune(pick, "position") == 4 && p.Inventory.Count == 0, "ordinary equipment handler equips the purchased tool durably");
        if (adventure) AdventureNear(p, 10080); else { p.Entity.X = town.Npcs[10080].X; p.Entity.Y = town.Npcs[10080].Y; }
        p.ActiveNpc = 10080;
        Parallel.For(0, 8, i => MineTravel(p));
        Check(!Fenced(p) && p.Entity.MapID == 1028 && p.Entity.Money == 1100
            && Scalar("SELECT trips FROM revora_plain_mine_passages WHERE uid=@uid AND NOT pending", p.Entity.UID) == 1,
            "existing Conductress mine option enters Plain Mine for exactly one durable 100-silver fare");
        StartMining(p); MineTick(p);
        Check(p.Mining && MineCount(p) == 0, "mining intent cannot grant an immediate reward");
        miningNow = miningNow.AddMilliseconds(1999); StartMining(p); MineTick(p);
        Check(MineCount(p) == 0, "repeated native start cannot shorten the two-second interval");
        miningNow = miningNow.AddMilliseconds(1); Parallel.For(0, 12, i => MineTick(p));
        Check(!Fenced(p) && MineCount(p) == 1 && p.Inventory.Count == 1 && p.Inventory.Objects[0].ID == 700001, "concurrent ticks yield one owned normal Phoenix Gem at the exact boundary");
        for (int i = 1; i < 3; i++) { miningGem = i; miningNow = miningNow.AddSeconds(2); MineTick(p); }
        if (adventure) { AdventureGo(p, 1002); AdventureGo(p, 1011); AdventureNear(p, 125); }
        else { MinePortal(p, 1002); MinePortal(p, 1011); p.Entity.X = phoenix.Npcs[125].X; p.Entity.Y = phoenix.Npcs[125].Y; }
        p.ActiveNpc = 125;
        Npcs.GetDialog(new NpcRequest(true) { NpcID = 125, OptionID = 1 }, p); Check(p.Entity.MapID == 1025, "installed Forest Mine assistant admits an ordinary visitor"); StartMining(p);
        for (int i = 2; i < 4; i++) { miningGem = i; miningNow = miningNow.AddSeconds(2); MineTick(p); }
        if (adventure) { AdventureGo(p, 1011); AdventureGo(p, 1020); AdventureNear(p, 126); }
        else { MinePortal(p, 1011); MinePortal(p, 1020); p.Entity.X = ape.Npcs[126].X; p.Entity.Y = ape.Npcs[126].Y; }
        p.ActiveNpc = 126;
        Npcs.GetDialog(new NpcRequest(true) { NpcID = 126, OptionID = 1 }, p); Check(p.Entity.MapID == 1026, "installed Canyon Mine assistant admits an ordinary visitor"); StartMining(p);
        // The two-entry pool uses a separate deterministic selector.
        MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => max == 2 ? miningGem : 0));
        for (int i = 0; i < 2; i++) { miningGem = i; miningNow = miningNow.AddSeconds(2); MineTick(p); }
        Check(!Fenced(p) && MineCount(p) == 7 && p.Inventory.Objects.Select(i => i.ID).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, 7).Select(i => 700001u + (uint)i * 10))
            && p.Inventory.Objects.All(i => !i.Bound && i.StackSize == 1), "three distinct mine pools earn all seven unbound normal recipe gems");
        if (adventure) AdventureBegin(runtime, town, p); else { World(runtime, 1212); p.Entity.Teleport(1212, 517, 732); }
        var mob = WaterMonster(p); mob.Hitpoints = mob.MaxHitpoints = 100; mob.IsDropped = false; mob.Die(p.Entity);
        Loot(p, p.Map.FloorItems.Values.Single(i => i.ItemID == 721258));
        if (adventure) AdventureReturn(p); else p.Entity.Teleport(1002, town.Npcs[59558].X, town.Npcs[59558].Y); p.ActiveNpc = 59558; StoneNpc(p, 38); StoneNpc(p, 39);
        Check(!Fenced(p) && MineCount(p) == 7 && Waters(p) == 1 && Stones(p) == 1 && p.Inventory.Count == 1 && p.Inventory.Objects[0].ID == 721259,
            "purchased tool, seven mined gems and hunted Clean Water complete the real Heavenly Stone craft");
        var fresh = ReloadGold(p); ConquerItemTable.LoadItems(fresh); new TitleStorage().Login(fresh);
        Check(!Fenced(fresh) && fresh.Entity.Money == (adventure ? 100ul : 1100ul) && fresh.Inventory.Objects.Single().ID == 721259 && fresh.Equipment.TryGetItem(4).UID == pick.UID
            && MineCount(fresh) == 7 && Stones(fresh) == 1 && !HasAchievement(fresh, 11030), "full production reload preserves purse, equipped tool and crafted stone without falsely awarding first rebirth");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,
            "mining, Clean Water pickup, Heavenly Stone crafting and production reload preserve wardrobe storage");
        if (adventure) {
            Check(new[] { 10715, 10716, 10717, 10718 }.All(id => HasAchievement(fresh, id))
                && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending", fresh.Entity.UID) == 1,
                "production reconnect preserves all four earned island visits and the consumed paid passage");
            VerifyAdventureGuards(runtime, town);
            fresh.ActiveNpc = 59558; RebirthOffer(fresh, 161); StoneNpc(fresh, 214);
            Check(!Fenced(fresh) && fresh.Entity.Reborn == 1 && fresh.Entity.Class == 161 && fresh.Entity.Level == 15 && fresh.Entity.Money == 100
                && Rebirths(fresh) == 1 && HasAchievement(fresh, 11030) && HasAchievement(fresh, 10315) && fresh.Inventory.Contains(700003, 1),
                "full walked purchase/mining/island/hunt/craft journey completes Windwalker first rebirth through ordinary Sereth dialogue");
            var reborn = ReloadGold(fresh); ConquerItemTable.LoadItems(reborn); new TitleStorage().Login(reborn);
            Check(!Fenced(reborn) && reborn.Entity.Reborn == 1 && reborn.Entity.FirstRebornClass == 15 && reborn.Entity.FirstRebornLevel == 135
                && reborn.Entity.Money == 100 && MineCount(reborn) == 7 && Waters(reborn) == 1 && Stones(reborn) == 1 && Rebirths(reborn) == 1
                && new[] { 10715,10716,10717,10718,11030,10315 }.All(id => HasAchievement(reborn, id)),
                "full production reload preserves every material provenance, paid crossing, final rite and six earned achievements");
        }
        VerifyMiningPools(runtime, plain, forest, canyon); VerifyMiningGuards(plain); VerifyMiningFailures(plain); VerifyPickaxeFailures(town); VerifyMinePassage(town);
    }
    static void VerifyMiningPools(string runtime, Map plain, Map forest, Map canyon) {
        var maps = new[] { plain, forest, canyon, World(runtime, 1027), World(runtime, 6000), World(runtime, 6001), TravelAlias(runtime, 1218, 1025) };
        foreach (var map in maps) {
            uint[] pool = map.ID == 1028 ? new uint[] { 700001, 700011, 700021 } : map.ID == 1026 || map.ID == 1027 ? new uint[] { 700051, 700061 } : new uint[] { 700001, 700011, 700031, 700041 };
            var p = Miner(map, map.ID == 6000 ? 562001u : 562000u); StartMining(p);
            for (int j = 0; j < pool.Length; j++) { int index = j;
                MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => max == pool.Length ? index : 0));
                miningNow = miningNow.AddSeconds(2); MineTick(p);
            }
            Check(!Fenced(p) && p.Inventory.Objects.Select(i => i.ID).SequenceEqual(pool), "owned mining tool yields only its installed physical map's selected normal-gem pool: " + map.ID);
        }
        foreach (int draw in new[] { 99, 100, 9999 }) {
            var p = Miner(plain); MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => max == 10000 ? draw : max == 100 ? 99 : 0));
            StartMining(p); miningNow = miningNow.AddSeconds(2); MineTick(p);
            Check(MineCount(p) == (draw < 100 ? 1 : 0), "normal gem one-percent boundary: " + draw);
            MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => 0)); MineTick(p);
            Check(MineCount(p) == (draw < 100 ? 1 : 0), "miss or success cannot be rerolled in the same interval: " + draw);
        }
        var ore = Miner(plain); Config.MiningNormalGemBasisPoints = 0; MiningService.GetField("Roll", Hidden).SetValue(null, (Func<int, int>)(max => 0));
        StartMining(ore); miningNow = miningNow.AddSeconds(2); MineTick(ore);
        Check(!Fenced(ore) && ore.Inventory.Objects.Single().ID == 1072031, "disabling normal gems preserves the existing Euxenite ore branch");
        foreach(var copperMap in new[]{forest,Kernel.Maps[1218]}) {
            var copper=Miner(copperMap);StartMining(copper);
            for(int grade=0;grade<10;grade++) {
                int selectedGrade=grade;
                MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==10?selectedGrade:0));
                miningNow=miningNow.AddSeconds(2);MineTick(copper);
                uint type=1072020u+(uint)grade;
                var item=copper.Inventory.Objects.Single(i=>i.ID==type);var saved=ConquerItemTable.LoadItem(item.UID);
                Check(!Fenced(copper) && saved!=null && saved.ID==type && !saved.Bound && MineCount(copper)==grade+1,
                    "Maple Forest copper grade persists through owned mining: "+copperMap.ID+"/"+(grade+1));
                MineTick(copper);
                Check(MineCount(copper)==grade+1,"copper yield cannot bypass the mining interval: "+copperMap.ID+"/"+(grade+1));
            }
        }
        foreach(var boundary in new[]{new[]{39,29,1},new[]{40,0,0},new[]{39,30,0}}) {
            var copper=Miner(forest);int draw=0;
            MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?(++draw==1?boundary[0]:draw==2?boundary[1]:99):0));
            StartMining(copper);miningNow=miningNow.AddSeconds(2);MineTick(copper);
            Check(!Fenced(copper) && copper.Inventory.Objects.Count(i=>i.ID>=1072020 && i.ID<=1072029)==boundary[2],
                "copper ore selection boundary: "+boundary[0]+"/"+boundary[1]);
        }
        Config.MiningNormalGemBasisPoints = 100;
    }
    static void VerifyMiningGuards(Map plain) {
        foreach (string mode in new[] { "wrong-uid", "fake", "unloaded", "dead", "trade", "booth", "poker", "level", "weapon", "broken", "suspicious", "warehouse", "stack", "wardrobe", "temporary", "inactive", "moved", "teleport", "full", "forged-flag", "stale" }) {
            var p = Miner(plain); var tool = p.Equipment.TryGetItem(4); var packet = MiningPacket(p);
            if (mode == "wrong-uid") { var request = new Data(true) { ID = Data.SwingPickaxe, UID = p.Entity.UID + 1 }; packet = request.ToArray(); Writer.WriteUInt64(PacketHandler.ClientSeal, packet.Length - 8, packet); }
            if (mode == "fake") p.Fake = true; if (mode == "unloaded") p.Entity.FullyLoaded = false; if (mode == "dead") p.Entity.Hitpoints = 0;
            if (mode == "trade") p.Trade.InTrade = true; if (mode == "booth") p.Booth = new Revora.Game.ConquerStructures.Booth(); if (mode == "poker") p.Entity.PokerTable = 1;
            if (mode == "level") p.Entity.Level = 69; if (mode == "weapon") tool.ID = 410009; if (mode == "broken") tool.Durability = 0;
            if (mode == "suspicious") tool.Suspicious = true; if (mode == "warehouse") tool.Warehouse = 1002; if (mode == "stack") tool.StackSize = 2;
            if (mode == "wardrobe") tool.InWardrobe = true; if (mode == "temporary") tool.Days = 1; if (mode == "inactive") p.AlternateEquipment = true;
            if (mode == "stale") Kernel.GamePool.Remove(p.Entity.UID);
            if (mode == "full") for (int i = 0; i < 40; i++) Pack(p, 1088001, 1);
            if (mode == "forged-flag") p.Mining = true; else Data.Process(packet, p);
            if (mode == "moved") p.Entity.X++; if (mode == "teleport") p.Entity.Teleport(p.Entity.X, p.Entity.Y);
            miningNow = miningNow.AddSeconds(2); MineTick(p);
            Check(!p.Mining && MineCount(p) == 0 && p.Inventory.Objects.All(i => i.ID == 1088001), "invalid or interrupted mining grants no item: " + mode);
            p.Booth = null;
        }
        foreach (string mode in new[] { "short", "length", "family", "seal", "oversize", "protobuf" }) {
            var p = Miner(plain); var bytes = MiningPacket(p);
            if (mode == "short") Array.Resize(ref bytes, 11); if (mode == "length") bytes[0]--; if (mode == "family") bytes[2]++;
            if (mode == "seal") bytes[bytes.Length - 1] ^= 1; if (mode == "oversize") Array.Resize(ref bytes, 65544); if (mode == "protobuf") bytes[4] = 0xFF;
            Data.Process(bytes, p); miningNow = miningNow.AddSeconds(2); MineTick(p);
            Check(!p.Mining && MineCount(p) == 0, "malformed action frame is rejected: " + mode);
        }
    }
    static void VerifyMiningFailures(Map plain) {
        foreach (string mode in new[] { "owner", "tool", "position", "durability", "map", "bag", "items", "revora_mining_state" }) {
            var p = Miner(plain); var tool = p.Equipment.TryGetItem(4); StartMining(p); bool trigger = mode == "items" || mode == "revora_mining_state";
            if (mode == "map") Query("UPDATE entities SET mapid=1002 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            else if (mode == "bag") Query("INSERT INTO items(uid,id,entityid) VALUES(@uid,1088001,@owner)", "uid", (long)ConquerItem.ItemUID.Next, "owner", (long)p.Entity.UID);
            else if (!trigger) Query("UPDATE items SET " + (mode == "owner" ? "entityid=1" : mode == "tool" ? "id=562001" : mode == "position" ? "position=0" : "durability=0") + " WHERE uid=@uid", "uid", (long)tool.UID);
            else Query("CREATE FUNCTION suppress_mining() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_mining BEFORE INSERT ON " + mode + " FOR EACH ROW EXECUTE FUNCTION suppress_mining()");
            miningNow = miningNow.AddSeconds(2);
            try { MineTick(p); } finally { if (trigger) Query("DROP TRIGGER suppress_mining ON " + mode + ";DROP FUNCTION suppress_mining()"); }
            Check(Fenced(p) && !p.Mining && MineCount(p) == 0 && p.Inventory.Count == 0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 700001 AND 700061", p.Entity.UID) == 0,
                "stale state or suppressed mining write rolls back its entire yield: " + mode);
            if (trigger) { DemonReset(p); StartMining(p); miningNow = miningNow.AddSeconds(2); MineTick(p); Check(!Fenced(p) && MineCount(p) == 1, "repaired mining write can retry once: " + mode); }
        }
        var durable = Miner(plain); StartMining(durable); miningNow = miningNow.AddSeconds(2); MineTick(durable);
        Query("UPDATE revora_mining_state SET next_at=next_at+interval '5 seconds' WHERE uid=@uid", "uid", (long)durable.Entity.UID);
        miningNow = miningNow.AddSeconds(2); MineTick(durable);
        Check(!Fenced(durable) && MineCount(durable) == 1, "durable next-yield boundary also rejects an early session tick");
        miningNow = miningNow.AddSeconds(5); MineTick(durable);
        Check(!Fenced(durable) && MineCount(durable) == 2 && Scalar("SELECT count(*) FROM revora_mining_state WHERE uid=@uid", durable.Entity.UID) == 1,
            "later yields update one bounded per-player state row");
        var fresh = ReloadGold(durable); ConquerItemTable.LoadItems(fresh); MineTick(fresh);
        Check(!fresh.Mining && MineCount(fresh) == 2 && fresh.Inventory.Count == 2, "reconnect restores mined items without automatically resuming an old mining intent");
        StartMining(fresh); miningNow = miningNow.AddSeconds(2); MineTick(fresh); MineTick(durable);
        Check(!Fenced(fresh) && MineCount(fresh) == 3 && fresh.Inventory.Count == 3 && !durable.Mining,
            "fresh canonical session can resume mining while the replaced session cannot yield");
        var alternate = Miner(plain); var alternateTool = alternate.Equipment.TryGetItem(4); ItemHandler.UnEquipItem(alternate, 4);
        ItemHandler.EquipItem(new ItemUsage(true) { UID = alternateTool.UID, dwParam = 24, Action = ItemUsage.Mode.EquipItem }, alternate);
        alternate.AlternateEquipment = true; StartMining(alternate); miningNow = miningNow.AddSeconds(2); MineTick(alternate);
        Check(!Fenced(alternate) && alternate.Equipment.TryGetItem(24) == alternateTool && SavedRune(alternateTool, "position") == 24 && MineCount(alternate) == 1,
            "ordinary equipment handler supports an owned tool in the selected alternate right-hand slot");
        alternate.AlternateEquipment = false; miningNow = miningNow.AddSeconds(2); MineTick(alternate);
        Check(!alternate.Mining && MineCount(alternate) == 1, "switching away from the active mining tool stops the session");
    }
    static GameState MineVisitor(Map town) {
        var p = StonePlayer(town); p.Entity.X = town.Npcs[10080].X; p.Entity.Y = town.Npcs[10080].Y; p.ActiveNpc = 10080;
        p.Entity.AdjustMoney(500); EntityTable.UpdateMoney(p); return p;
    }
    static void VerifyMinePassage(Map town) {
        foreach (string mode in new[] { "funds", "level", "pk", "far", "trade", "fake", "unloaded", "stale", "receipt", "purse", "missing-map" }) {
            var p = MineVisitor(town); var mine = Kernel.Maps[1028];
            if (mode == "funds") { p.Entity.AdjustMoney(-401); EntityTable.UpdateMoney(p); }
            if (mode == "level") p.Entity.Level = 69; if (mode == "pk") p.Entity.PKPoints = 29; if (mode == "far") p.Entity.X += 10;
            if (mode == "trade") p.Trade.InTrade = true; if (mode == "fake") p.Fake = true; if (mode == "unloaded") p.Entity.FullyLoaded = false;
            if (mode == "stale") Query("UPDATE entities SET money=700 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            if (mode == "missing-map") Kernel.Maps.Remove(1028);
            bool trigger = mode == "receipt" || mode == "purse"; string table = mode == "receipt" ? "revora_plain_mine_passages" : "entities";
            if (trigger) Query("CREATE FUNCTION suppress_mine_fare() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_mine_fare BEFORE " + (mode == "receipt" ? "INSERT" : "UPDATE") + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_mine_fare()");
            try { MineTravel(p); }
            finally { if (trigger) Query("DROP TRIGGER suppress_mine_fare ON " + table + ";DROP FUNCTION suppress_mine_fare()"); if (mode == "missing-map") Kernel.Maps[1028] = mine; }
            Check(p.Entity.MapID == 1002 && Scalar("SELECT count(*) FROM revora_plain_mine_passages WHERE uid=@uid", p.Entity.UID) == 0
                && Scalar("SELECT money FROM entities WHERE uid=@uid", p.Entity.UID) == (mode == "funds" ? 99 : mode == "stale" ? 700 : 500),
                "ineligible visitor or failed fare transaction preserves the complete purse: " + mode);
        }
        var interrupted = MineVisitor(town);
        Query("ALTER TABLE entities ADD CONSTRAINT reject_mine_arrival CHECK(mapid<>1028) NOT VALID");
        try { MineTravel(interrupted); } finally { Query("ALTER TABLE entities DROP CONSTRAINT reject_mine_arrival"); }
        Check(Fenced(interrupted) && interrupted.Entity.MapID == 1002 && interrupted.Entity.Money == 400
            && Scalar("SELECT trips FROM revora_plain_mine_passages WHERE uid=@uid AND pending", interrupted.Entity.UID) == 1,
            "interrupted map transition preserves one paid resumable mine passage");
        var fresh = ReloadGold(interrupted); fresh.Entity.X = town.Npcs[10080].X; fresh.Entity.Y = town.Npcs[10080].Y; fresh.ActiveNpc = 10080;
        Parallel.For(0, 8, i => MineTravel(fresh));
        Check(!Fenced(fresh) && fresh.Entity.MapID == 1028 && fresh.Entity.Money == 400
            && Scalar("SELECT trips FROM revora_plain_mine_passages WHERE uid=@uid AND NOT pending", fresh.Entity.UID) == 1,
            "production reconnect resumes the already paid mine trip without another charge");
        fresh.Entity.Teleport(1002, town.Npcs[10080].X, town.Npcs[10080].Y); fresh.ActiveNpc = 10080; MineTravel(fresh);
        Check(!Fenced(fresh) && fresh.Entity.MapID == 1028 && fresh.Entity.Money == 300
            && Scalar("SELECT trips FROM revora_plain_mine_passages WHERE uid=@uid AND NOT pending", fresh.Entity.UID) == 2,
            "a later intentional trip pays a new fare and updates one bounded passage row");
    }
    static void VerifyPickaxeFailures(Map town) {
        foreach (string mode in new[] { "funds", "range", "stock", "stale", "items", "revora_weapon_book_purchases" }) {
            var p = StonePlayer(town); p.Entity.X = town.Npcs[1].X; p.Entity.Y = town.Npcs[1].Y; p.Entity.AdjustMoney(mode == "funds" ? 999 : 2000); EntityTable.UpdateMoney(p);
            if (mode == "range") p.Entity.X += 20; if (mode == "stock") ShopFile.Shops[1].Items.Remove(562000);
            if (mode == "stale") Query("UPDATE entities SET money=3000 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            bool trigger = mode == "items" || mode == "revora_weapon_book_purchases";
            if (trigger) Query("CREATE FUNCTION suppress_pickaxe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_pickaxe BEFORE INSERT ON " + mode + " FOR EACH ROW EXECUTE FUNCTION suppress_pickaxe()");
            try { ItemHandler.HandleBuyFromNPC(BuyBook(562000, 1, 1), p); }
            finally { if (trigger) Query("DROP TRIGGER suppress_pickaxe ON " + mode + ";DROP FUNCTION suppress_pickaxe()"); if (mode == "stock") ShopFile.Shops[1].Items.Add(562000); }
            Check(p.Inventory.Count == 0 && BookPurchases(p) == 0 && Scalar("SELECT money FROM entities WHERE uid=@uid", p.Entity.UID) == (mode == "funds" ? 999 : mode == "stale" ? 3000 : 2000),
                "rejected Pickaxe purchase preserves durable purse and bag: " + mode);
        }
    }
}
