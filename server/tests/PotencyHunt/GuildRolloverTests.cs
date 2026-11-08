using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Store=Revora.Game.ConquerStructures.Warehouse;

internal static partial class PotencyHuntTests
{
    static readonly Type GuildRollover=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildPrizeRollover");
    static void RollGuild(GameState p){Call(GuildRollover,null,"Apply",p);}
    static long Rolled(GameState p){return Scalar("SELECT count(*) FROM revora_guild_prizes WHERE uid=@uid AND rolled_at IS NOT NULL AND trophy_uid=item_uid",p.Entity.UID);}
    static DateTime GoldEnd(GameState p) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT ends_at FROM revora_guild_prizes WHERE uid=@uid AND tier=3",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);using(var r=q.ExecuteReader()){r.Read();return r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}}}
    }
    static GameState EarnGold(uint id,DateTime first) {
        var p=PrizeLeader(id);for(int i=0;i<3;i++){var day=first.AddDays(7*i);EarnGuildToken(p,day.Month,day.Day,day.Year);}
        GuildPrize(p,84);GuildPrize(p,85);if(Fenced(p) || p.Inventory.Objects.Single().ID!=2100075)throw new Exception("Gold earning fixture failed");return p;
    }
    static void PlaceGold(GameState p,ConquerItem item,int place) {
        if(place==0)return;p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        if(place==7 || place==27){item.Position=(byte)place;p.Equipment.Add(item,Enums.ItemUse.None);item.IsWorn=true;Query("UPDATE items SET position=@pos WHERE uid=@uid","pos",place,"uid",(long)item.UID);}
        else {if(p.Warehouses==null)p.Warehouses=new System.Collections.Generic.SafeDictionary<Store.WarehouseID,Store>();
            var wh=new Store(p,Store.WarehouseID.TwinCity);p.Warehouses[Store.WarehouseID.TwinCity]=wh;wh.Add(item);Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)item.UID);}
    }
    static GameState ReloadGold(GameState p) {
        var fresh=TravelPlayer(Kernel.Maps[1002],317,322);uint placeholder=fresh.Entity.UID;fresh.Account.EntityID=p.Entity.UID;
        fresh.Account.Username="guild_rollover_fixture";fresh.Quests=new Quests(fresh);
        if(!(bool)Call(typeof(EntityTable),null,"LoadEntityState",fresh))throw new Exception("Rollover entity reload failed");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[p.Entity.UID]=fresh;fresh.Action=2;fresh.Entity.FullyLoaded=true;
        if(fresh.Warehouses==null)fresh.Warehouses=new System.Collections.Generic.SafeDictionary<Store.WarehouseID,Store>();
        fresh.Warehouses[Store.WarehouseID.TwinCity]=new Store(fresh,Store.WarehouseID.TwinCity);return fresh;
    }
    static void VerifyGuildRollover(GameState gold,GameState silver) {
        Call(GuildRollover,null,"Initialize");Call(GuildRollover,null,"Initialize");var prize=gold.Inventory.Objects.Single();var end=GoldEnd(gold);
        guildNow=end.AddTicks(-1);RollGuild(gold);Check(prize.ID==2100075 && Rolled(gold)==0,"Gold Prize remains seasonal until its exact recorded boundary");
        guildNow=end;foreach(string table in new[]{"items","revora_guild_prizes"}) {
            Query("CREATE FUNCTION suppress_guild_rollover() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_rollover BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guild_rollover()");
            try{RollGuild(gold);}finally{Query("DROP TRIGGER suppress_guild_rollover ON "+table+";DROP FUNCTION suppress_guild_rollover()");}
            Check(Fenced(gold) && prize.ID==2100075 && Rolled(gold)==0 && ConquerItemTable.LoadItem(prize.UID).ID==2100075,"suppressed "+table+" rollover write preserves the original prize and unconsumed provenance");DemonReset(gold);
        }
        gold.Trade.InTrade=true;RollGuild(gold);gold.Trade.InTrade=false;Check(Rolled(gold)==0,"active trade defers online conversion until the owner is available");
        Kernel.GamePool[gold.Entity.UID]=silver;RollGuild(gold);Kernel.GamePool[gold.Entity.UID]=gold;Check(Rolled(gold)==0,"replaced online session cannot convert another owner's prize");
        PlaceGold(gold,prize,7);gold.LoadItemStats();uint cp=gold.Entity.ConquerPoints;Parallel.For(0,8,i=>RollGuild(gold));
        Check(!Fenced(gold) && Rolled(gold)==1 && prize.ID==2100085 && prize.Days==0 && prize.Position==7 && gold.Equipment.TryGetItem((byte)7)==prize
            && gold.Inventory.Count==0 && gold.Entity.ConquerPoints==cp,"equipped earned Gold Prize converts once in its existing slot without another CP payment or bag slot");
        var loaded=ConquerItemTable.LoadItem(prize.UID);Check(loaded.ID==2100085 && loaded.Position==7 && loaded.Bound && loaded.Days==0,"production item loading preserves the permanent equipped trophy and exact UID");
        uint hp=gold.Entity.MaxHitpoints,attack=gold.Entity.MaxAttack;gold.LoadItemStats();gold.CalculateHPBonus();
        Check(gold.Entity.MaxHitpoints==hp && gold.Entity.MaxAttack==attack,"online rollover leaves equipped combat statistics equal to a fresh recalculation");
        var loose=Pack(silver,2100075,1);loose.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)loose.UID);guildNow=GoldEnd(gold).AddYears(2);RollGuild(silver);
        Check(loose.ID==2100075 && silver.Inventory.Objects.Any(i=>i.ID==2100065) && Rolled(silver)==0,"unrecorded Gold Prizes and earned Silver Prizes are untouched by Gold rollover");
        var stored=EarnGold(208,new DateTime(2026,12,13));var storedPrize=stored.Inventory.Objects.Single();PlaceGold(stored,storedPrize,8);guildNow=GoldEnd(stored);
        for(int i=0;i<40;i++)Pack(stored,1088001,1);RollGuild(stored);
        Check(!Fenced(stored) && Rolled(stored)==1 && stored.Inventory.Count==40 && storedPrize.ID==2100085 && storedPrize.Warehouse==8
            && stored.Warehouses[Store.WarehouseID.TwinCity].GetItem(storedPrize.UID)==storedPrize,"warehouse rollover updates its owned object and saved row even with a full inventory");
        DisRemove(stored,stored.Inventory.Objects.First());
        Warehouse.Process(StorageFrame(2,storedPrize.UID),stored);
        Check(stored.Inventory.ContainsUID(storedPrize.UID) && storedPrize.ID==2100085 && storedPrize.Warehouse==0 && ConquerItemTable.LoadItem(storedPrize.UID).ID==2100085,
            "ordinary warehouse withdrawal uses the converted trophy object and preserves its saved identity");
        foreach(int place in new[]{0,7,8}) {
            uint id=(uint)(209+(place==0?0:place==7?1:2));DateTime first=place==0?new DateTime(2027,1,3):place==7?new DateTime(2027,2,7):new DateTime(2027,3,14);
            var offline=EarnGold(id,first);var original=offline.Inventory.Objects.Single();PlaceGold(offline,original,place);guildNow=GoldEnd(offline);Kernel.GamePool.Remove(offline.Entity.UID);
            Call(GuildRollover,null,"Tick");Check(Rolled(offline)==0 && ConquerItemTable.LoadItem(original.UID).ID==2100075,"offline "+place+" prize waits for the character's owned load transaction");
            var returning=ReloadGold(offline);ConquerItemTable.LoadItems(returning);
            var found=place==0?returning.Inventory.Objects.Single():place==7?returning.Equipment.TryGetItem((byte)7):returning.Warehouses[Store.WarehouseID.TwinCity].GetItem(original.UID);
            Check(!Fenced(returning) && Rolled(returning)==1 && found.UID==original.UID && found.ID==2100085 && found.Bound && found.Days==0,
                "ordinary character reload converts its offline "+place+" prize before loading inventory, equipment or warehouse objects");
            var second=ReloadGold(returning);ConquerItemTable.LoadItems(second);Check(Rolled(second)==1 && ConquerItemTable.LoadItem(original.UID).ID==2100085,"repeated offline "+place+" reload cannot mint another trophy");
        }
        var polled=EarnGold(212,new DateTime(2027,4,18));var pollItem=polled.Inventory.Objects.Single();guildNow=GoldEnd(polled);
        GuildRollover.GetField("nextPoll",Hidden).SetValue(null,DateTime.MinValue);GuildRollover.GetField("cursor",Hidden).SetValue(null,0L);Call(GuildRollover,null,"Tick");
        Check(!Fenced(polled) && Rolled(polled)==1 && pollItem.ID==2100085,"scheduled bounded online poll converts a due earned inventory prize");
        var interrupted=EarnGold(213,new DateTime(2027,5,23));var pending=interrupted.Inventory.Objects.Single();guildNow=GoldEnd(interrupted);
        var wrong=ReloadGold(interrupted);wrong.Account.EntityID++;bool failed=false;
        try{ConquerItemTable.LoadItems(wrong);}catch(InvalidOperationException){failed=true;}
        Check(failed && Fenced(wrong) && Rolled(wrong)==0 && wrong.Inventory.Count==0,"mismatched login account cannot convert or load another character's earned prize");
        foreach(string table in new[]{"items","revora_guild_prizes"}) {
            var returning=ReloadGold(interrupted);failed=false;
            Query("CREATE FUNCTION suppress_guild_load() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_load BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guild_load()");
            try{ConquerItemTable.LoadItems(returning);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER suppress_guild_load ON "+table+";DROP FUNCTION suppress_guild_load()");}
            Check(failed && Fenced(returning) && Rolled(returning)==0 && returning.Inventory.Count==0 && ConquerItemTable.LoadItem(pending.UID).ID==2100075,
                "suppressed offline "+table+" conversion aborts item loading and preserves the entire pending reward");
        }
        var committed=ReloadGold(interrupted);Call(GuildRollover,null,"BeforeLoad",committed);
        Check(Rolled(committed)==1 && committed.Inventory.Count==0 && ConquerItemTable.LoadItem(pending.UID).ID==2100085,
            "offline conversion can commit before any runtime item projection exists");
        var recovered=ReloadGold(interrupted);ConquerItemTable.LoadItems(recovered);
        Check(!Fenced(recovered) && Rolled(recovered)==1 && recovered.Inventory.Objects.Single().UID==pending.UID && recovered.Inventory.Objects.Single().ID==2100085,
            "fresh ordinary login recovers a committed conversion with a missing prior projection without issuing another trophy");
        var moving=EarnGold(214,new DateTime(2027,6,27));var movingPrize=moving.Inventory.Objects.Single();PlaceGold(moving,movingPrize,8);guildNow=GoldEnd(moving);
        Parallel.For(0,24,i=> {
            if(i%3==0)RollGuild(moving);
            else Warehouse.Process(StorageFrame(i%3==1?(byte)2:(byte)1,movingPrize.UID),moving);
        });RollGuild(moving);
        var savedMoving=ConquerItemTable.LoadItem(movingPrize.UID);
        Check(!Fenced(moving) && Rolled(moving)==1 && movingPrize.ID==2100085 && savedMoving.ID==2100085 && savedMoving.Warehouse==movingPrize.Warehouse
            && moving.Inventory.Objects.Concat(moving.Warehouses[Store.WarehouseID.TwinCity].Objects).Count(i=>i.UID==movingPrize.UID)==1,
            "concurrent native warehouse moves and rollover retain one trophy with matching saved and runtime ownership");
        const uint wideKey=1800000000;
        var wide=EarnGold(217,new DateTime(2026,9,20).AddDays(35*60));var widePrize=wide.Inventory.Objects.Single();
        if(wide.Warehouses==null)wide.Warehouses=new System.Collections.Generic.SafeDictionary<Store.WarehouseID,Store>();
        RegisterStorage(wide,wideKey);Warehouse.Process(StorageFrame(1,widePrize.UID,wideKey),wide);
        Check(!Fenced(wide) && widePrize.Warehouse==wideKey && !wide.Inventory.ContainsUID(widePrize.UID),"earned seasonal prize enters full-width storage before rollover");
        guildNow=GoldEnd(wide);var returningWide=ReloadGold(wide);RegisterStorage(returningWide,wideKey);ConquerItemTable.LoadItems(returningWide);
        var fullStored=returningWide.Warehouses[(Store.WarehouseID)wideKey].GetItem(widePrize.UID);
        Check(!Fenced(returningWide) && Rolled(returningWide)==1 && fullStored.ID==2100085 && fullStored.Warehouse==wideKey,
            "offline prize rollover preserves full-width storage key before production item hydration");
        Warehouse.Process(StorageFrame(2,widePrize.UID,wideKey),returningWide);
        Check(!Fenced(returningWide) && returningWide.Inventory.ContainsUID(widePrize.UID)
            && ConquerItemTable.LoadItem(widePrize.UID).Warehouse==0,"rolled trophy withdraws from its full-width storage identity");
    }
}

