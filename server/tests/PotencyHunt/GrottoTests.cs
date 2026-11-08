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
    static readonly Type Passage=typeof(Entity).Assembly.GetType("Revora.Game.Features.GrottoPassage");
    static long Passages(GameState p) {return Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid",p.Entity.UID);}
    static uint StoredQuestGarment(GameState p) {
        if(p.Entity.StorageItems==null)Collection(p);
        int count=p.Inventory.Count;var garment=Pack(p,187405,1);
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=garment.UID}}.Handle(p);
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Takeoff,ItemGuid=garment.UID}}.Handle(p);
        Check(!Fenced(p) && p.Inventory.Count==count && p.Entity.StorageItems.ContainsKey(garment.UID)
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",garment.UID)==1,
            "ordinary wardrobe deposit and takeoff store a supplied garment outside the quest player's bag");
        return garment.UID;
    }
    static GameState GrottoPlayer(Map map) {
        var p=TravelPlayer(map,map.ID==1999?(ushort)640:(ushort)388,map.ID==1999?(ushort)333:(ushort)460);p.Entity.VIPLevel=0;
        if(!EntityTable.SaveEntity(p))throw new Exception("Grotto fixture save failed");return p;
    }
    static void GrottoBack(GameState p) {p.Entity.X=531;p.Entity.Y=575;ExplorationNpc(p,7765,4);}
    static void VerifyGrotto(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType,null,"Initialize");Call(Travel,null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ArmorDyeShop"),null,"Initialize");
        Call(Passage,null,"Initialize");Call(Passage,null,"Initialize");
        var fourth=World(runtime,2054);var fifth=World(runtime,2055);World(runtime,2056);
        fourth.Npcs[7764]=new NpcSpawn{UID=7764,MapID=2054,X=388,Y=460};fifth.Npcs[7765]=new NpcSpawn{UID=7765,MapID=2055,X=531,Y=575};
        ConquerItem.ItemUID=new Counter(6100000);
        var p=GrottoPlayer(fourth);var key=Pack(p,721347,3);ulong gold=p.Entity.Money;uint cps=p.Entity.ConquerPoints;
        p.ActiveNpc=7764;Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=7764,OptionID=3},p));
        Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID==2055 && key.StackSize==2
            && Scalar("SELECT stacksize FROM items WHERE uid=@uid",key.UID)==2 && Passages(p)==1 && HasAchievement(p,10805),"ordinary key admission consumes one stack unit once and earns the actual fifth-floor visit");
        Check(p.Entity.Money==gold && p.Entity.ConquerPoints==cps && Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid AND entered_at IS NOT NULL AND closed_at IS NULL",p.Entity.UID)==1,"successful entry records its arrival without unrelated currency changes");
        p.Entity.Teleport(2056,364,648);p.Entity.Teleport(2055,522,554);
        Check(Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid AND closed_at IS NULL",p.Entity.UID)==1,"fifth-to-sixth-floor travel preserves the same paid visit");
        GrottoBack(p);Check(p.Entity.MapID==2054 && Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid AND closed_at IS NOT NULL",p.Entity.UID)==1,"return Guardian goes to floor four and closes the completed visit");
        ExplorationNpc(p,7764,3);Check(!Fenced(p) && key.StackSize==1 && Passages(p)==2,"a deliberate new visit consumes exactly one more key");
        GrottoBack(p);ExplorationNpc(p,7764,3);
        Check(p.Entity.MapID==2055 && p.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",key.UID)==0 && Passages(p)==3,"last stack unit is removed from database and inventory together");
        var poor=GrottoPlayer(fourth);ExplorationNpc(poor,7764,3);ExplorationNpc(poor,7764,4);
        Check(poor.Entity.MapID==2054 && Passages(poor)==0,"a non-VIP without a key cannot enter through either confirmation");
        var vip=GrottoPlayer(fourth);vip.Entity.VIPLevel=4;var spare=Pack(vip,721347,1);ExplorationNpc(vip,7764,4);
        Check(!Fenced(vip) && vip.Entity.MapID==2055 && spare.StackSize==1 && Passages(vip)==1
            && Scalar("SELECT kind FROM revora_grotto_passages WHERE uid=@uid",vip.Entity.UID)==2,"existing VIP-4 free admission preserves carried keys");
        GrottoBack(vip);ExplorationNpc(vip,7764,4);Check(Passages(vip)==2 && vip.Inventory.Count==1,"existing Revora VIP repeat-entry policy is preserved");
        vip.Entity.Teleport(1002,424,339);
        Check(Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid AND closed_at IS NULL",vip.Entity.UID)==0,
            "leaving the upper floors for another map closes the visit alongside the armor-dye trigger");
        foreach(string state in new[]{"far","dead","fake","trade","replaced"}) {
            var f=GrottoPlayer(fourth);var k=Pack(f,721347,1);
            if(state=="far")f.Entity.X=10;if(state=="dead")f.Entity.Hitpoints=0;if(state=="fake")f.Fake=true;
            if(state=="trade")f.Trade.InTrade=true;if(state=="replaced")Kernel.GamePool.Remove(f.Entity.UID);
            ExplorationNpc(f,7764,3);Check(f.Entity.MapID==2054 && Passages(f)==0 && f.Inventory.Count==1 && Scalar("SELECT stacksize FROM items WHERE uid=@uid",k.UID)==1,"ineligible "+state+" request preserves its key");
        }
        var missing=GrottoPlayer(fourth);Pack(missing,721347,1);var info=MapsTable.MapInformations[2055];MapsTable.MapInformations.Remove(2055);
        try{ExplorationNpc(missing,7764,3);}finally{MapsTable.MapInformations[2055]=info;}
        Check(missing.Entity.MapID==2054 && Passages(missing)==0 && missing.Inventory.Count==1,"missing destination is rejected before key payment");
        foreach(string fault in new[]{"foreign","stack","bound","locked","receipt"}) {
            var f=GrottoPlayer(fourth);var k=Pack(f,721347,2);
            if(fault=="foreign")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)k.UID);
            if(fault=="stack")Query("UPDATE items SET stacksize=3 WHERE uid=@uid","uid",(long)k.UID);
            if(fault=="bound")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)k.UID);
            if(fault=="locked")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)k.UID);
            if(fault=="receipt")Query("CREATE FUNCTION suppress_passage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_passage BEFORE INSERT ON revora_grotto_passages FOR EACH ROW EXECUTE FUNCTION suppress_passage()");
            try{ExplorationNpc(f,7764,3);}finally{if(fault=="receipt")Query("DROP TRIGGER suppress_passage ON revora_grotto_passages;DROP FUNCTION suppress_passage()");}
            Check(Fenced(f) && f.Entity.MapID==2054 && k.StackSize==2 && Passages(f)==0
                && Scalar("SELECT stacksize FROM items WHERE uid=@uid",k.UID)==(fault=="stack"?3:2),"failed "+fault+" preserves key and rolls back passage");
        }
        var interrupted=GrottoPlayer(fourth);var interruptedKey=Pack(interrupted,721347,2);
        Query("ALTER TABLE entities ADD CONSTRAINT reject_grotto_arrival CHECK(mapid<>2055) NOT VALID");
        try{ExplorationNpc(interrupted,7764,3);}finally{Query("ALTER TABLE entities DROP CONSTRAINT reject_grotto_arrival");}
        Check(Fenced(interrupted) && interrupted.Entity.MapID==2054 && interruptedKey.StackSize==1 && Passages(interrupted)==1
            && Scalar("SELECT count(*) FROM revora_grotto_passages WHERE uid=@uid AND entered_at IS NULL AND closed_at IS NULL",interrupted.Entity.UID)==1,"failed map transition retains the committed key debit and a resumable passage");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        var resumed=GrottoPlayer(fourth);uint placeholder=resumed.Entity.UID;resumed.Account.EntityID=interrupted.Entity.UID;resumed.Account.Username="grotto_passage_fixture";resumed.Quests=new Quests(resumed);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",resumed),"fresh ordinary character load recovers an interrupted paid passage");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[interrupted.Entity.UID]=resumed;resumed.Action=2;ConquerItemTable.LoadItems(resumed);
        resumed.Entity.X=388;resumed.Entity.Y=460;ExplorationNpc(resumed,7764,3);
        Check(!Fenced(resumed) && resumed.Entity.MapID==2055 && resumed.Inventory.Count==1 && resumed.Inventory.Objects[0].StackSize==1
            && Passages(resumed)==1 && HasAchievement(resumed,10805),"reconnected player resumes beside the Guardian without paying a second key");
        Check(EntityTable.SaveEntity(resumed) && Scalar("SELECT stacksize FROM items WHERE uid=@uid",interruptedKey.UID)==1,"subsequent character save does not refund the consumed key");
        VerifyGrottoKeys(runtime,fourth,town);
        VerifyDragonCrafts(runtime);
        VerifyDragonHunt(runtime);
    }

    static readonly Type DragonCraft=typeof(Entity).Assembly.GetType("Revora.Game.Features.DragonPillCrafting");
    static long DragonCrafts(GameState p){return Scalar("SELECT count(*) FROM revora_dragon_pill_crafts WHERE uid=@uid",p.Entity.UID);}
    static GameState DragonPlayer(Map map){var p=TravelPlayer(map,361,645);if(!EntityTable.SaveEntity(p))throw new Exception("Dragon fixture save failed");return p;}
    static ConquerItem[] GhoulPills(GameState p,ushort stack=1){return Enumerable.Range(711181,7).Select(i=>Pack(p,(uint)i,stack)).ToArray();}
    static void DragonNpc(GameState p,byte option){ExplorationNpc(p,7767,option);}
    static void VerifyDragonCrafts(string runtime) {
        Call(DragonCraft,null,"Initialize");Call(DragonCraft,null,"Initialize");var sixth=Kernel.Maps[2056];World(runtime,1000);
        sixth.Npcs[7767]=new NpcSpawn{UID=7767,MapID=2056,X=361,Y=645};DateTime now=DateTime.UtcNow;
        DragonCraft.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        var p=DragonPlayer(sixth);var pills=GhoulPills(p);ulong gold=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
        DragonNpc(p,0);DragonNpc(p,6);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=7767,OptionID=60},p));
        Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Inventory.Count==1 && p.Inventory.Objects[0].ID==720598
            && !p.Inventory.Objects[0].Bound && DragonCrafts(p)==1 && pills.All(i=>Scalar("SELECT count(*) FROM items WHERE uid=@uid",i.UID)==0),
            "ordinary Guardian crafting consumes seven distinct pills and creates one tradable Dragon Pill once");
        Check(p.Entity.Money==gold && p.Entity.ConquerPoints==cp
            && Scalar("SELECT sum(m.amount) FROM revora_dragon_pill_materials m JOIN revora_dragon_pill_crafts c ON c.id=m.craft WHERE c.uid=@uid",p.Entity.UID)==7,
            "Dragon Pill craft records every consumed material without charging currency");
        Check(EntityTable.SaveEntity(p) && ConquerItemTable.LoadItem(p.Inventory.Objects[0].UID).ID==720598,"crafted Dragon Pill survives character save and item reload");
        var fragments=DragonPlayer(sixth);var stack=Pack(fragments,728120,16);DragonNpc(fragments,17);DragonNpc(fragments,60);DragonNpc(fragments,60);
        Check(!Fenced(fragments) && stack.StackSize==8 && Scalar("SELECT stacksize FROM items WHERE uid=@uid",stack.UID)==8 && DragonCrafts(fragments)==1
            && fragments.Inventory.Count==2,"eight-fragment recipe preserves a partial stack and repeated confirmation cannot charge it again");
        DragonNpc(fragments,17);DragonNpc(fragments,60);
        Check(DragonCrafts(fragments)==2 && fragments.Inventory.Count==2 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",stack.UID)==0,
            "a fresh confirmed recipe can consume the final eight fragments");
        var mixed=DragonPlayer(sixth);var bound=Pack(mixed,728120,3);bound.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)bound.UID);
        var loose=Pack(mixed,728120,7);DragonNpc(mixed,17);DragonNpc(mixed,60);
        Check(!Fenced(mixed) && mixed.Inventory.Count==2 && loose.StackSize==2 && mixed.Inventory.Objects.Single(i=>i.ID==720598).Bound
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",bound.UID)==0,"split-stack recipe spends bound fragments first and preserves binding on its result");
        var mixedPills=DragonPlayer(sixth);var set=GhoulPills(mixedPills,2);set[4].Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)set[4].UID);
        DragonNpc(mixedPills,6);DragonNpc(mixedPills,60);
        Check(!Fenced(mixedPills) && set.All(i=>i.StackSize==1) && mixedPills.Inventory.Objects.Single(i=>i.ID==720598).Bound,
            "seven-pill recipe consumes only one per type and cannot launder bound inputs");
        var incomplete=DragonPlayer(sixth);for(uint i=711181;i<711187;i++)Pack(incomplete,i,1);DragonNpc(incomplete,6);DragonNpc(incomplete,60);
        Check(incomplete.Inventory.Count==6 && DragonCrafts(incomplete)==0,"missing one ghoul pill preserves the other six");
        var shortStack=DragonPlayer(sixth);Pack(shortStack,728120,7);DragonNpc(shortStack,17);DragonNpc(shortStack,60);
        Check(shortStack.Inventory.Objects[0].StackSize==7 && DragonCrafts(shortStack)==0,"seven fragments are insufficient and remain unspent");
        var noOffer=DragonPlayer(sixth);Pack(noOffer,728120,8);DragonNpc(noOffer,60);
        Check(noOffer.Inventory.Count==1 && DragonCrafts(noOffer)==0,"raw craft confirmation requires a fresh material offer");
        var expired=DragonPlayer(sixth);Pack(expired,728120,8);DragonNpc(expired,17);now=now.AddMinutes(3);DragonNpc(expired,60);
        Check(expired.Inventory.Count==1 && DragonCrafts(expired)==0,"expired crafting offer leaves its materials untouched");
        var canceled=DragonPlayer(sixth);Pack(canceled,728120,8);DragonNpc(canceled,17);DragonNpc(canceled,255);DragonNpc(canceled,60);
        Check(canceled.Inventory.Count==1 && DragonCrafts(canceled)==0,"canceling the offer prevents later replay");
        DragonNpc(canceled,17);Npcs.GetDialog(new NpcRequest(true){NpcID=0,OptionID=255},canceled);DragonNpc(canceled,60);
        Check(canceled.Inventory.Count==1 && DragonCrafts(canceled)==0,"native zero-NPC cancellation clears the crafting offer");
        var changed=DragonPlayer(sixth);var changedStack=Pack(changed,728120,9);DragonNpc(changed,17);changedStack.StackSize=8;DragonNpc(changed,60);
        Check(!Fenced(changed) && DragonCrafts(changed)==0,"changed in-memory material selection requires a new offer");
        foreach(string state in new[]{"far","dead","fake","trade","replaced","warehouse","locked","expired-item"}) {
            var f=DragonPlayer(sixth);var material=Pack(f,728120,8);
            if(state=="far")f.Entity.X=10;if(state=="dead")f.Entity.Hitpoints=0;if(state=="fake")f.Fake=true;
            if(state=="trade")f.Trade.InTrade=true;if(state=="replaced")Kernel.GamePool.Remove(f.Entity.UID);
            if(state=="warehouse")material.Warehouse=1002;if(state=="locked")material.Lock=1;
            if(state=="expired-item"){material.Days=1;material.DayStamp=now.ToLocalTime().AddDays(-2);}
            DragonNpc(f,17);DragonNpc(f,60);
            Check(DragonCrafts(f)==0 && Scalar("SELECT stacksize FROM items WHERE uid=@uid",material.UID)==8,"ineligible Dragon Pill craft preserves materials: "+state);
        }
        var full=DragonPlayer(sixth);GhoulPills(full,2);for(int i=0;i<33;i++)Pack(full,1088001,1);DragonNpc(full,6);DragonNpc(full,60);
        Check(full.Inventory.Count==40 && DragonCrafts(full)==0,"full inventory rejects a recipe that would not free a slot");
        var space=DragonPlayer(sixth);GhoulPills(space);for(int i=0;i<33;i++)Pack(space,1088001,1);DragonNpc(space,6);DragonNpc(space,60);
        Check(!Fenced(space) && space.Inventory.Count==34 && DragonCrafts(space)==1,"full inventory may craft when its consumed materials free enough slots");
        foreach(string fault in new[]{"foreign","stack","bound","locked","item","receipt","material-receipt"}) {
            var f=DragonPlayer(sixth);var material=Pack(f,728120,8);DragonNpc(f,17);
            if(fault=="foreign")Query("UPDATE items SET entityid=1 WHERE uid=@uid","uid",(long)material.UID);
            if(fault=="stack")Query("UPDATE items SET stacksize=9 WHERE uid=@uid","uid",(long)material.UID);
            if(fault=="bound")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)material.UID);
            if(fault=="locked")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)material.UID);
            string table=fault=="item"?"items":fault=="receipt"?"revora_dragon_pill_crafts":fault=="material-receipt"?"revora_dragon_pill_materials":null;
            if(table!=null)Query("CREATE FUNCTION reject_dragon_craft() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER reject_dragon_craft BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_dragon_craft()");
            try{DragonNpc(f,60);}finally{if(table!=null)Query("DROP TRIGGER reject_dragon_craft ON "+table+";DROP FUNCTION reject_dragon_craft()");}
            Check(Fenced(f) && DragonCrafts(f)==0 && f.Inventory.Count==1 && material.StackSize==8
                && Scalar("SELECT stacksize FROM items WHERE uid=@uid",material.UID)==(fault=="stack"?9:8)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=720598",f.Entity.UID)==0,
                "craft rolls back its material debit and result on "+fault);
        }
        var reconnect=DragonPlayer(sixth);uint temp=reconnect.Entity.UID;reconnect.Account.EntityID=p.Entity.UID;reconnect.Account.Username="dragon_crafting_fixture";reconnect.Quests=new Quests(reconnect);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reconnect),"ordinary crafter reconnects through the real character-state loader");
        Kernel.GamePool.Remove(temp);Kernel.GamePool[p.Entity.UID]=reconnect;reconnect.Action=2;ConquerItemTable.LoadItems(reconnect);
        Check(reconnect.Inventory.Count==1 && reconnect.Inventory.Objects[0].ID==720598 && DragonCrafts(reconnect)==1,
            "fresh character and inventory load preserve the crafted pill and its receipt");
        DragonNpc(reconnect,60);Check(reconnect.Inventory.Count==1 && DragonCrafts(reconnect)==1,"new session cannot replay an old crafting confirmation");
        DragonNpc(reconnect,15);DragonNpc(reconnect,16);Check(reconnect.Entity.MapID==1000,"Guardian preserves the existing Desert City return route");
        var house=TravelAlias(runtime,(ushort)reconnect.Entity.UID,2056);Kernel.Maps[2056]=sixth;reconnect.Entity.MapID=house.ID;
        reconnect.ActiveNpc=123456;var crafted=reconnect.Inventory.Objects[0];ItemHandler.UseItem(crafted,reconnect);
        Check(reconnect.ActiveNpc==123456 && reconnect.Inventory.ContainsUID(crafted.UID) && DragonCrafts(reconnect)==1,
            "Dragon Pill use cannot fall through into Sword Master's house confirmation");
        reconnect.Entity.MapID=1002;reconnect.Entity.X=517;reconnect.Entity.Y=742;var town=Kernel.Maps[1002];
        town.Npcs[180810]=new NpcSpawn{UID=180810,MapID=1002,X=517,Y=742,Mesh=8630};var meteors=Pack(reconnect,1088001,5);
        bool spawning=Kernel.Spawn;Kernel.Spawn=false;int oldMonsters=town.Entities.Count;
        try{ExplorationNpc(reconnect,180810,1);}finally{Kernel.Spawn=spawning;}
        Check(reconnect.Inventory.ContainsUID(crafted.UID) && meteors.StackSize==5 && town.Entities.Count==oldMonsters,
            "Royal Guardian offer selection preserves materials until a fresh confirmation");
    }

    static readonly Type GrottoLoot=typeof(Entity).Assembly.GetType("Revora.Game.Features.GrottoKeyDrops");
    static FloorItem GrottoDrop(GameState p) {
        bool found=false;ushort x=p.Entity.X,y=p.Entity.Y;
        for(int radius=0;radius<40 && !found;radius++)for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
            int nx=x+dx,ny=y+dy;if(nx<0 || ny<0 || nx>ushort.MaxValue || ny>ushort.MaxValue)continue;
            if(p.Map.Floor[(ushort)nx,(ushort)ny,MapObjectType.Item,null]){p.Entity.X=(ushort)nx;p.Entity.Y=(ushort)ny;found=true;}
        }
        if(!found)throw new Exception("Grotto fixture has no free hunting tile");
        var before=p.Map.FloorItems.Keys.ToArray();Call(GrottoLoot,null,"TryDrop",LootMonster(p),p.Entity);
        return p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID));
    }
    static long KeyReceipts(GameState p) {return Scalar("SELECT count(*) FROM revora_grotto_key_pickups WHERE collector=@uid",p.Entity.UID);}
    static void VerifyGrottoKeys(string runtime,Map fourth,Map town) {
        Call(GrottoLoot,null,"Initialize");Call(GrottoLoot,null,"Initialize");
        GrottoLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));Config.GrottoKeyBasisPoints=100;
        var third=World(runtime,1999);
        foreach(uint family in new uint[]{2460,2461,2466}) {
            var map=family==2466?fourth:third;var actualHunter=GrottoPlayer(map);var info=MonsterInformation.MonsterInformations[family].Copy();
            var mob=LootMonster(actualHunter);mob.MonsterInfo=info;info.Owner=mob;mob.Name=info.Name;int before=map.FloorItems.Count;
            Call(GrottoLoot,null,"TryDrop",mob,actualHunter.Entity);
            Check(!info.Boss && !info.Guard && !info.Reviver && map.FloorItems.Count==before+1,"installed "+info.Name+" profile qualifies for Grotto Key earning");
        }
        foreach(var map in new[]{third,fourth}) {
            var farmer=GrottoPlayer(map);StoredQuestGarment(farmer);var drop=GrottoDrop(farmer);Loot(farmer,drop);var saved=ConquerItemTable.LoadItem(drop.Item.UID);
            Check(!Fenced(farmer) && drop.PickedUpAlready && !map.FloorItems.ContainsKey(drop.UID) && farmer.Inventory.Count==1
                && saved.ID==721347 && !saved.Bound && saved.StackSize==1 && saved.Plus==0 && KeyReceipts(farmer)==1,
                "ordinary floor "+map.ID+" monster loot yields a durable tradable Grotto Key through the pickup dispatcher");
            Loot(farmer,drop);Call(GrottoLoot,null,"Pickup",farmer,drop);
            Check(farmer.Inventory.Count==1 && KeyReceipts(farmer)==1,"repeated key pickup does not duplicate the item or receipt");
        }
        var earned=GrottoPlayer(fourth);var earnedDrop=GrottoDrop(earned);Loot(earned,earnedDrop);uint earnedUid=earnedDrop.Item.UID;
        Check(EntityTable.SaveEntity(earned),"a collected Grotto Key survives the full character save");
        var reloaded=GrottoPlayer(fourth);uint temporary=reloaded.Entity.UID;reloaded.Account.EntityID=earned.Entity.UID;
        reloaded.Account.Username="grotto_earned_key_fixture";reloaded.Quests=new Quests(reloaded);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reloaded),"an ordinary hunter reconnects with a genuinely earned key");
        Kernel.GamePool.Remove(temporary);Kernel.GamePool[earned.Entity.UID]=reloaded;reloaded.Action=2;ConquerItemTable.LoadItems(reloaded);
        reloaded.Entity.X=388;reloaded.Entity.Y=460;ExplorationNpc(reloaded,7764,3);
        Check(!Fenced(reloaded) && reloaded.Entity.MapID==2055 && reloaded.Inventory.Count==0 && Passages(reloaded)==1 && KeyReceipts(reloaded)==1
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",earnedUid)==0 && HasAchievement(reloaded,10805),
            "hunt, floor pickup, character reload, key payment and fifth-floor exploration complete the ordinary earning route");
        var racing=GrottoPlayer(fourth);var shared=GrottoDrop(racing);racing.Entity.X=shared.X;racing.Entity.Y=shared.Y;
        Parallel.For(0,12,i=>FloorItem.Process(LootPacket(shared),racing));
        Check(!Fenced(racing) && racing.Inventory.Count==1 && KeyReceipts(racing)==1,"parallel key pickups grant exactly one item");
        var direct=GrottoPlayer(fourth);var directDrop=GrottoDrop(direct);direct.Entity.X=directDrop.X;direct.Entity.Y=directDrop.Y;direct.Screen.Add(directDrop);
        ItemHandler.PickupItem(new FloorItem(true){UID=directDrop.UID},direct);
        Check(!Fenced(direct) && direct.Inventory.Count==1 && KeyReceipts(direct)==1,"direct server pickup dispatch uses the same atomic key transaction");
        var rival=GrottoPlayer(fourth);var protectedDrop=GrottoDrop(racing);Loot(rival,protectedDrop);
        Check(rival.Inventory.Count==0 && !protectedDrop.PickedUpAlready,"Grotto Keys honor the killer's ownership window");
        protectedDrop.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);Loot(rival,protectedDrop);
        Check(!Fenced(rival) && rival.Inventory.Count==1 && KeyReceipts(rival)==1,"after ownership expires a different ordinary collector receives the key");
        var contest=GrottoDrop(racing);contest.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemAvailableAfter);
        Task.WaitAll(Task.Run(()=>Loot(racing,contest)),Task.Run(()=>Loot(rival,contest)));
        Check(!Fenced(racing) && !Fenced(rival) && racing.Inventory.Count+rival.Inventory.Count==3 && KeyReceipts(racing)+KeyReceipts(rival)==3,
            "two collectors racing for one key cannot duplicate it");
        var hunter=GrottoPlayer(fourth);var life=LootMonster(hunter);int count=fourth.FloorItems.Count;
        GrottoLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>100));Call(GrottoLoot,null,"TryDrop",life,hunter.Entity);
        GrottoLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));Call(GrottoLoot,null,"TryDrop",life,hunter.Entity);
        Check(fourth.FloorItems.Count==count,"failed 1% boundary roll cannot be retried for the same death");
        life.DeathStamp=life.DeathStamp.AddSeconds(1);Call(GrottoLoot,null,"TryDrop",life,hunter.Entity);Call(GrottoLoot,null,"TryDrop",life,hunter.Entity);
        Check(fourth.FloorItems.Count==count+1,"a new monster life rolls once for a new key");
        GrottoLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>99));count=fourth.FloorItems.Count;Call(GrottoLoot,null,"TryDrop",LootMonster(hunter),hunter.Entity);
        Check(fourth.FloorItems.Count==count+1,"roll 99 succeeds at the configured 100-basis-point boundary");
        GrottoLoot.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));
        foreach(string mode in new[]{"alive","boss","guard","reviver","companion","unregistered","wrong-map"}) {
            var mob=LootMonster(hunter);if(mode=="alive")mob.Hitpoints=1;if(mode=="boss")mob.MonsterInfo.Boss=true;
            if(mode=="guard")mob.MonsterInfo.Guard=true;if(mode=="reviver")mob.MonsterInfo.Reviver=true;if(mode=="companion")mob.Companion=true;
            if(mode=="unregistered")fourth.Entities.Remove(mob.UID);if(mode=="wrong-map")mob.MapID=1002;
            count=fourth.FloorItems.Count;Call(GrottoLoot,null,"TryDrop",mob,hunter.Entity);
            Check(fourth.FloorItems.Count==count,"ineligible Grotto monster cannot yield a key: "+mode);
        }
        var outside=GrottoPlayer(town);count=town.FloorItems.Count;Call(GrottoLoot,null,"TryDrop",LootMonster(outside),outside.Entity);
        Check(town.FloorItems.Count==count,"ordinary monsters outside floors three/four do not drop Grotto Keys");
        var alias=TravelAlias(runtime,12055,2054);var copied=GrottoPlayer(alias);count=alias.FloorItems.Count;Call(GrottoLoot,null,"TryDrop",LootMonster(copied),copied.Entity);
        Check(alias.FloorItems.Count==count,"a copied Grotto terrain instance is not an earning source");
        Kernel.Maps[2054]=fourth;
        foreach(int chance in new[]{0,-1}) {
            Config.GrottoKeyBasisPoints=chance;count=fourth.FloorItems.Count;Call(GrottoLoot,null,"TryDrop",LootMonster(hunter),hunter.Entity);
            Check(fourth.FloorItems.Count==count,"nonpositive Grotto chance disables key drops");
        }
        Config.GrottoKeyBasisPoints=100;
        var actual=LootMonster(hunter);count=fourth.FloorItems.Count;bool money=Config.MoneyDropEnabled;Config.MoneyDropEnabled=false;
        try{actual.MonsterInfo.Drop(hunter.Entity);}finally{Config.MoneyDropEnabled=money;}
        Check(fourth.FloorItems.Count==count+1,"production monster death dispatch reaches Grotto Key earning");
        var frameDrop=GrottoDrop(hunter);
        foreach(string mode in new[]{"short","length","family","seal","oversize"}) {
            byte[] frame=LootPacket(frameDrop);if(mode=="short")Array.Resize(ref frame,12);if(mode=="length")frame[0]--;
            if(mode=="family")frame[2]++;if(mode=="seal")frame[frame.Length-1]^=1;if(mode=="oversize")Array.Resize(ref frame,129);
            hunter.Entity.X=frameDrop.X;hunter.Entity.Y=frameDrop.Y;FloorItem.Process(frame,hunter);
            Check(!Fenced(hunter) && !frameDrop.PickedUpAlready && hunter.Inventory.Count==0,"malformed key pickup frame is rejected: "+mode);
        }
        hunter.Entity.X=(ushort)(frameDrop.X+1);FloorItem.Process(LootPacket(frameDrop),hunter);
        Check(!frameDrop.PickedUpAlready,"request coordinates cannot replace the collector's real position");
        var expired=GrottoDrop(hunter);expired.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemSeconds);Loot(hunter,expired);
        Check(!expired.PickedUpAlready,"expired Grotto Keys cannot be recovered by stale pickup");
        var removed=GrottoDrop(hunter);fourth.RemoveFloorItem(removed);Loot(hunter,removed);
        Check(!removed.PickedUpAlready && !hunter.Inventory.ContainsUID(removed.Item.UID),"removed keys cannot be recovered from stale floor references");
        var replacement=new FloorItem(true){UID=removed.UID,MapID=fourth.ID,X=removed.X,Y=removed.Y,Type=FloorItem.Drop};fourth.AddFloorItem(replacement);fourth.RemoveFloorItem(removed);
        Check(ReferenceEquals(fourth.FloorItems[replacement.UID],replacement),"stale key expiry cannot remove a replacement floor object");fourth.RemoveFloorItem(replacement);
        var full=GrottoPlayer(fourth);for(int n=0;n<40;n++)Pack(full,1088001,1);var waiting=GrottoDrop(full);Loot(full,waiting);
        Check(!Fenced(full) && full.Inventory.Count==40 && !waiting.PickedUpAlready && KeyReceipts(full)==0,"full inventory preserves the key on the ground");
        foreach(string mode in new[]{"item","receipt","silent-item","silent-receipt"}) {
            var f=GrottoPlayer(fourth);var drop=GrottoDrop(f);string table=mode.EndsWith("item")?"items":"revora_grotto_key_pickups";
            string effect=mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture key failure'';";
            Query("CREATE FUNCTION fail_key() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_key BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_key()");
            try{Loot(f,drop);}finally{Query("DROP TRIGGER fail_key ON "+table+";DROP FUNCTION fail_key()");}
            Check(Fenced(f) && f.Inventory.Count==0 && !drop.PickedUpAlready && fourth.FloorItems.ContainsKey(drop.UID) && KeyReceipts(f)==0
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",drop.Item.UID)==0,"key and receipt roll back together: "+mode);
            DemonReset(f);Loot(f,drop);Check(!Fenced(f) && f.Inventory.Count==1 && KeyReceipts(f)==1,"still-live key can be collected after recovering from "+mode);
        }
        var stale=GrottoPlayer(fourth);var hidden=Pack(stale,1088001,1);stale.Inventory.Remove(hidden.UID,Enums.ItemUse.None,false);var staleDrop=GrottoDrop(stale);Loot(stale,staleDrop);
        Check(Fenced(stale) && !staleDrop.PickedUpAlready && KeyReceipts(stale)==0,"stale inventory image cannot accept a key");
        var altered=GrottoPlayer(fourth);var changed=GrottoDrop(altered);changed.Item.Bound=true;Loot(altered,changed);
        Check(Fenced(altered) && !changed.PickedUpAlready && KeyReceipts(altered)==0,"mutated key provenance is rejected before persistence");
    }
}
