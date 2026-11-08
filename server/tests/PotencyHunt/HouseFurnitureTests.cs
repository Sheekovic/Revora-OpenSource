using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Furniture=typeof(Entity).Assembly.GetType("Revora.Game.Features.HouseFurniture");
    static void FurnitureNpc(GameState p,byte option){AdventureNpc(p,30161,option);}
    static byte[] FurnitureFrame(ushort id,params uint[] pairs)
    {
        var body=new List<byte>();
        for(int i=0;i<pairs.Length;i+=2){body.Add((byte)(pairs[i]*8));uint v=pairs[i+1];while(v>=128){body.Add((byte)(v|128));v>>=7;}body.Add((byte)v);}
        var bytes=new byte[body.Count+12];body.CopyTo(bytes,4);Writer.WriteUInt16((ushort)(bytes.Length-8),0,bytes);Writer.WriteUInt16(id,2,bytes);
        Writer.WriteUInt64(PacketHandler.ClientSeal,bytes.Length-8,bytes);return bytes;
    }
    static byte[] FurniturePosition(GameState p,uint type)
    {
        ushort look=type==721189?(ushort)8200:(ushort)((type-721176)*10);
        for(int dx=-6;dx<=6;dx++)for(int dy=-6;dy<=6;dy++){
            int x=p.Entity.X+dx,y=p.Entity.Y+dy;
            if(x>0 && y>0 && x<p.Map.Floor.Bounds.Width && y<p.Map.Floor.Bounds.Height
                && (bool)Call(Furniture,null,"Cell",p,(ushort)x,(ushort)y,look))
                return FurnitureFrame(2030,4,(uint)x,5,(uint)y,6,look);
        }
        throw new Exception("No furniture fixture cell.");
    }
    static void RemoveFurniture(GameState p,uint actor){PacketHandler.HandlePacket(FurnitureFrame(2031,1,6,2,actor,3,0,4,0),p);}
    static long FurnitureCount(GameState p){return Scalar("SELECT count(*) FROM revora_house_furniture WHERE uid=@uid AND removed_at IS NULL",p.Entity.UID);}
    static GameState FurnitureOwner(Map town)
    {
        var p=HousePlayer(town);
        for(int level=1;level<=2;level++){BuyHouseCert(p,level);HouseNpc(p,level==1?(byte)1:(byte)3);HouseNpc(p,40);}
        HouseNpc(p,2);return p;
    }
    static GameState StoragePlayer(Map town)
    {
        var p=HousePlayer(town);
        p.Warehouses=new SafeDictionary<Revora.Game.ConquerStructures.Warehouse.WarehouseID,Revora.Game.ConquerStructures.Warehouse>();
        p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity]=new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity);
        return p;
    }
    static byte[] StorageFrame(byte action,uint item,uint storage=8)
    {
        var b=new byte[172];Writer.WriteUInt16(164,0,b);Writer.WriteUInt16(1102,2,b);
        Writer.WriteUInt32(storage,4,b);b[12]=action;b[13]=10;Writer.WriteUInt32(item,18,b);
        Writer.WriteUInt64(PacketHandler.ClientSeal,164,b);return b;
    }
    static Revora.Game.ConquerStructures.Warehouse TownStorage(GameState p)
    {return p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity];}
    static void StoreRequest(GameState p,byte action,uint item){Warehouse.Process(StorageFrame(action,item),p);}
    static void VerifyWarehouseTransfers(Map town)
    {
        var p=StoragePlayer(town);p.SpiltStack=true;p.Entity.UseItem=true;var stack=Pack(p,1088001,7);var other=Pack(p,1088001,3);
        uint uid=stack.UID,otherUid=other.UID;
        Parallel.For(0,8,i=>StoreRequest(p,1,uid));
        Check(!Fenced(p) && !p.Inventory.ContainsUID(uid) && TownStorage(p).GetItem(uid).StackSize==7
            && p.Inventory.Objects.Single().UID==otherUid && Scalar("SELECT warehouse FROM items WHERE uid=@uid",uid)==8,
            "concurrent native deposit moves the whole stack once without merging or consuming a unit");
        p=ReloadBall(p);p.SpiltStack=true;
        Check(TownStorage(p).GetItem(uid).StackSize==7 && p.Inventory.Objects.Single().StackSize==3,"deposit survives production item reload");
        Parallel.For(0,8,i=>StoreRequest(p,2,uid));
        Check(!Fenced(p) && TownStorage(p).Count==0 && p.Inventory.Count==2 && p.Inventory.Objects.Single(i=>i.UID==uid).StackSize==7
            && p.Inventory.Objects.Single(i=>i.UID==otherUid).StackSize==3 && Scalar("SELECT warehouse FROM items WHERE uid=@uid",uid)==0,
            "concurrent withdrawal preserves both stack identities and quantities with automatic stacking enabled");
        p=ReloadBall(p);Check(p.Inventory.Count==2 && TownStorage(p).Count==0,"withdrawal survives production item reload");
        foreach(bool deposit in new[]{true,false}){
            var f=StoragePlayer(town);var item=Pack(f,1088001,4);if(!deposit)StoreRequest(f,1,item.UID);
            Query("CREATE FUNCTION suppress_storage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_storage BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_storage()");
            try{StoreRequest(f,deposit?(byte)1:(byte)2,item.UID);}finally{Query("DROP TRIGGER suppress_storage ON items;DROP FUNCTION suppress_storage()");}
            Check(Fenced(f) && item.Warehouse==(deposit?0:8) && f.Inventory.ContainsUID(item.UID)==deposit
                && TownStorage(f).ContainsUID(item.UID)!=deposit && Scalar("SELECT warehouse FROM items WHERE uid=@uid",item.UID)==(deposit?0:8),
                "failed storage write leaves saved and cached source location unchanged: "+deposit);
            f=ReloadBall(f);StoreRequest(f,deposit?(byte)1:(byte)2,item.UID);
            Check(!Fenced(f) && f.Inventory.ContainsUID(item.UID)!=deposit && TownStorage(f).ContainsUID(item.UID)==deposit,
                "failed storage operation can be retried after reconnect: "+deposit);
        }
        var full=StoragePlayer(town);var saved=Pack(full,1088001,2);StoreRequest(full,1,saved.UID);
        for(int i=0;i<40;i++)Pack(full,1088001,1);
        StoreRequest(full,2,saved.UID);
        Check(!Fenced(full) && full.Inventory.Count==40 && saved.Warehouse==8 && TownStorage(full).ContainsUID(saved.UID)
            && Scalar("SELECT warehouse FROM items WHERE uid=@uid",saved.UID)==8,"full bag withdrawal never clears the stored item location");
        var small=StoragePlayer(town);TownStorage(small).MaxCount=1;var a=Pack(small,1088001,1);var b=Pack(small,1088001,1);
        StoreRequest(small,1,a.UID);StoreRequest(small,1,b.UID);
        Check(!Fenced(small) && TownStorage(small).Count==1 && small.Inventory.ContainsUID(b.UID) && b.Warehouse==0,"full storage preserves bag item");
        var stale=StoragePlayer(town);var changed=Pack(stale,1088001,2);
        Query("UPDATE items SET stacksize=3 WHERE uid=@uid","uid",(long)changed.UID);StoreRequest(stale,1,changed.UID);
        Check(Fenced(stale) && stale.Inventory.ContainsUID(changed.UID) && TownStorage(stale).Count==0
            && Scalar("SELECT stacksize FROM items WHERE uid=@uid",changed.UID)==3,"stale stack preimage cannot overwrite persisted item");
        var recipient=StoragePlayer(town);var stranger=StoragePlayer(town);var foreign=Pack(stranger,1088001,2);StoreRequest(recipient,1,foreign.UID);StoreRequest(recipient,2,foreign.UID);
        Check(stranger.Inventory.ContainsUID(foreign.UID) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",foreign.UID)==0,"another player's UID cannot be transferred");
        var trading=StoragePlayer(town);var held=Pack(trading,1088001,2);trading.Trade.InTrade=true;StoreRequest(trading,1,held.UID);trading.Trade.InTrade=false;
        Check(!Fenced(trading) && trading.Inventory.ContainsUID(held.UID) && held.Warehouse==0,"trade state blocks storage transfer");
        var decorated=StoragePlayer(town);var original=Pack(decorated,1088001,4);original.Plus=5;original.Bound=true;original.Durability=77;original.Enchant=10;
        Query("UPDATE items SET plus=5,bound=1,durability=77,enchant=10 WHERE uid=@uid","uid",(long)original.UID);
        StoreRequest(decorated,1,original.UID);decorated=ReloadBall(decorated);var loaded=TownStorage(decorated).GetItem(original.UID);
        Check(loaded.Plus==5 && loaded.Bound && loaded.Durability==77 && loaded.Enchant==10 && loaded.StackSize==4,"deposit reload preserves item attributes and binding");
        StoreRequest(decorated,2,original.UID);decorated=ReloadBall(decorated);loaded=decorated.Inventory.Objects.Single();
        Check(loaded.UID==original.UID && loaded.Plus==5 && loaded.Bound && loaded.Durability==77 && loaded.Enchant==10 && loaded.StackSize==4,
            "withdrawal reload preserves item attributes and binding");
        var divergence=StoragePlayer(town);var hidden=Pack(divergence,1088001,1);var waiting=Pack(divergence,1088001,1);TownStorage(divergence).MaxCount=1;
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)hidden.UID);StoreRequest(divergence,1,waiting.UID);
        Check(!Fenced(divergence) && waiting.Warehouse==0 && Scalar("SELECT warehouse FROM items WHERE uid=@uid",waiting.UID)==0,
            "persisted storage capacity prevents overflow even when runtime contents are stale");
        var ownership=StoragePlayer(town);var stolen=Pack(ownership,1088001,1);
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)stranger.Entity.UID,"uid",(long)stolen.UID);StoreRequest(ownership,1,stolen.UID);
        Check(Fenced(ownership) && stolen.Warehouse==0 && Scalar("SELECT entityid FROM items WHERE uid=@uid",stolen.UID)==stranger.Entity.UID,
            "persisted owner mismatch cannot transfer an item despite a stale bag reference");
        VerifyStorageIdentities(town);
    }
    static void RegisterStorage(GameState p,uint key)
    {
        var id=(Revora.Game.ConquerStructures.Warehouse.WarehouseID)key;
        p.Warehouses[id]=new Revora.Game.ConquerStructures.Warehouse(p,id);
    }
    static void VerifyStorageIdentities(Map town)
    {
        const uint first=1700000000,second=1700065536;
        var p=StoragePlayer(town);RegisterStorage(p,first);RegisterStorage(p,second);
        var a=Pack(p,1088001,2);var b=Pack(p,1088001,3);var untouched=Pack(p,1088001,4);
        Warehouse.Process(StorageFrame(1,a.UID,first),p);Warehouse.Process(StorageFrame(1,b.UID,second),p);
        Check(!Fenced(p) && a.Warehouse==first && b.Warehouse==second && TownStorage(p).Count==0
            && Scalar("SELECT warehouse FROM items WHERE uid=@uid",a.UID)==first && Scalar("SELECT warehouse FROM items WHERE uid=@uid",b.UID)==second,
            "full storage keys sharing their low sixteen bits retain separate saved contents");
        Warehouse.Process(StorageFrame(1,untouched.UID,65544),p);
        Check(p.Inventory.ContainsUID(untouched.UID) && TownStorage(p).Count==0,"unregistered high-bit alias cannot access Twin City storage");
        p=ReloadGold(p);RegisterStorage(p,first);RegisterStorage(p,second);ConquerItemTable.LoadItems(p);
        Check(p.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)first].GetItem(a.UID).StackSize==2
            && p.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)second].GetItem(b.UID).StackSize==3,
            "production item loader preserves registered full-width storage identities without truncation");
        Warehouse.Process(StorageFrame(2,a.UID,second),p);
        Check(!p.Inventory.ContainsUID(a.UID),"one registered storage key cannot withdraw another key's item");
        Warehouse.Process(StorageFrame(2,a.UID,first),p);Warehouse.Process(StorageFrame(2,b.UID,second),p);
        Check(!Fenced(p) && p.Inventory.Count==3 && p.Inventory.Objects.All(i=>i.Warehouse==0),"both full-width keys withdraw their own exact items");
        var legacy=StoragePlayer(town);var bag=Pack(legacy,1100003,1);var contents=Pack(legacy,1088001,5);
        Warehouse.Process(StorageFrame(1,contents.UID,bag.UID),legacy);
        Check(!Fenced(legacy) && contents.Warehouse==(ushort)bag.UID && !legacy.Inventory.ContainsUID(contents.UID),"owned portable bag retains its existing persisted numeric key");
        legacy=ReloadBall(legacy);Warehouse.Process(StorageFrame(2,contents.UID,bag.UID),legacy);
        Check(!Fenced(legacy) && legacy.Inventory.ContainsUID(contents.UID) && legacy.Inventory.Objects.Single(i=>i.UID==contents.UID).StackSize==5,
            "owned legacy portable bag contents survive production reload and native withdrawal");
        var account=StoragePlayer(town);account.Account.EntityID=account.Entity.UID;RegisterStorage(account,(ushort)account.Account.EntityID);
        var existing=Pack(account,1088001,1);Warehouse.Process(StorageFrame(1,existing.UID,account.Account.EntityID),account);
        Check(!Fenced(account) && existing.Warehouse==(ushort)account.Account.EntityID,"legacy account request retains its persisted numeric key");
        RegisterStorage(account,uint.MaxValue);var invalid=Pack(account,1088001,1);Warehouse.Process(StorageFrame(1,invalid.UID,uint.MaxValue),account);
        Check(!Fenced(account) && invalid.Warehouse==0 && account.Inventory.ContainsUID(invalid.UID),"key outside the signed database range cannot wrap into another storage key");
    }
    static void VerifyHouseFurniture(Map town)
    {
        VerifyWarehouseTransfers(town);
        VerifyFurnitureFootprints();
        MiningNpc(town,30161);Call(Furniture,null,"Initialize");
        VerifyHouseBoxes(town);
        var p=HousePlayer(town);p.Entity.ConquerPoints=48000;
        Query("UPDATE entities SET conquerpoints=48000 WHERE uid=@uid","uid",(long)p.Entity.UID);
        AdventureNear(p,30161);ulong start=p.Entity.Money;uint startCp=p.Entity.ConquerPoints;
        for(byte i=0;i<12;i++){FurnitureNpc(p,(byte)(10+i));Parallel.For(0,8,j=>FurnitureNpc(p,40));}
        Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Inventory.Count==12 && p.Entity.Money==start && p.Entity.ConquerPoints==startCp-30000
            && Scalar("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid AND cps_paid=2500 AND silver_paid=0",p.Entity.UID)==12,
            "ordinary furniture purchases deliver all twelve models with once-only 2500 CP charges");
        Call(Furniture,null,"Initialize");
        p=ReloadBall(p);Check(p.Inventory.Count==12 && p.Entity.Money==start && p.Entity.ConquerPoints==startCp-30000,"furniture purchases and CPs survive migration repeat and production reload");
        var poor=HousePlayer(town);poor.Entity.ConquerPoints=2499;poor.Entity.BoundCps=10000;EntityTable.SaveEntity(poor);
        AdventureNear(poor,30161);FurnitureNpc(poor,10);FurnitureNpc(poor,40);
        Check(!Fenced(poor) && poor.Inventory.Count==0 && poor.Entity.ConquerPoints==2499 && poor.Entity.BoundCps==10000,"silver and bound CPs cannot substitute for furniture CP price");
        poor.Entity.ConquerPoints=2500;EntityTable.SaveEntity(poor);FurnitureNpc(poor,10);FurnitureNpc(poor,40);
        Check(!Fenced(poor) && poor.Inventory.Count==1 && poor.Entity.ConquerPoints==0 && poor.Entity.BoundCps==10000,"exact 2500 CP balance purchases one decoration");
        var first=p.Inventory.Objects.First();ItemHandler.UseItem(first,p);
        Check(FurnitureCount(p)==0 && p.Inventory.Count==12,"furniture cannot be selected outside an owned home");
        AdventureNear(p,30156);BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);HouseNpc(p,2);
        ItemHandler.UseItem(first,p);PacketHandler.HandlePacket(FurniturePosition(p,first.ID),p);
        Check(FurnitureCount(p)==0 && p.Inventory.Count==12,"level-one home cannot accept decorative furniture");
        p.Entity.Teleport(1002,345,529);AdventureNear(p,30156);BuyHouseCert(p,2);HouseNpc(p,3);HouseNpc(p,40);HouseNpc(p,2);
        var materials=p.Inventory.Objects.OrderBy(i=>i.ID).ToArray();
        for(int i=0;i<8;i++){
            ItemHandler.UseItem(materials[i],p);var frame=FurniturePosition(p,materials[i].ID);Parallel.For(0,8,j=>PacketHandler.HandlePacket(frame,p));
            Check(!Fenced(p) && FurnitureCount(p)==i+1 && !p.Inventory.ContainsUID(materials[i].UID),"paid furnishing places once with owned item debit: "+materials[i].ID);
        }
        Check(HasAchievement(p,10101) && p.Inventory.Count==4,"actual placement awards the furnishing achievement");
        ItemHandler.UseItem(materials[8],p);PacketHandler.HandlePacket(FurniturePosition(p,materials[8].ID),p);
        Check(!Fenced(p) && FurnitureCount(p)==8 && p.Inventory.Count==4,"level-two furniture capacity preserves the ninth item");
        ushort home=p.Entity.MapID;p.Entity.Teleport(1002,345,529);Kernel.Maps[home].Dispose();MapsTable.MapInformations.Remove(home);
        Query("UPDATE entities SET mapid=@map WHERE uid=@uid","map",(int)home,"uid",(long)p.Entity.UID);
        p=ReloadBall(p);new TitleStorage().Login(p);
        Check(!Fenced(p) && p.Entity.MapID==home && p.Map.Npcs.Values.Count(n=>n.Type==Enums.NpcType.Furniture)==8 && HasAchievement(p,10101),
            "cold house reconstruction reloads all furniture and the saved achievement");
        var actors=p.Map.Npcs.Values.Where(n=>n.Type==Enums.NpcType.Furniture).Select(n=>n.UID).ToArray();
        var stranger=HousePlayer(town);RemoveFurniture(stranger,actors[0]);
        Check(FurnitureCount(p)==8,"another player cannot remove private furniture");
        for(int i=0;i<4;i++){RemoveFurniture(p,actors[i]);RemoveFurniture(p,actors[i]);}
        Check(!Fenced(p) && FurnitureCount(p)==4 && p.Inventory.Count==4,"removal destroys four furnishings without refunds or duplicate effects");
        foreach(var item in p.Inventory.Objects.ToArray()){ItemHandler.UseItem(item,p);PacketHandler.HandlePacket(FurniturePosition(p,item.ID),p);}
        Check(!Fenced(p) && FurnitureCount(p)==8 && p.Inventory.Count==0
            && Scalar("SELECT count(DISTINCT item_type) FROM revora_house_furniture WHERE uid=@uid",p.Entity.UID)==12,
            "all twelve purchased furniture models reach durable placement");
        foreach(var npc in p.Map.Npcs.Values.Where(n=>n.Type==Enums.NpcType.Furniture).ToArray())RemoveFurniture(p,npc.UID);
        p=ReloadBall(p);new TitleStorage().Login(p);
        Check(FurnitureCount(p)==0 && p.Inventory.Count==0 && HasAchievement(p,10101),"destructive removal persists without revoking earned achievement");
        foreach(string fault in new[]{"items","furniture","revora_house_furniture","revora_achievement_evidence"}){
            var f=FurnitureOwner(town);var item=Pack(f,721177,1);ItemHandler.UseItem(item,f);var frame=FurniturePosition(f,item.ID);
            string verb=fault=="items"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_furniture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_furniture BEFORE "+verb+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_furniture()");
            try{PacketHandler.HandlePacket(frame,f);}finally{Query("DROP TRIGGER suppress_furniture ON "+fault+";DROP FUNCTION suppress_furniture()");}
            Check(Fenced(f) && FurnitureCount(f)==0 && ConquerItemTable.LoadItem(item.UID).ID==721177
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10101",f.Entity.UID)==0,
                "failed placement write preserves material and prevents achievement: "+fault);
            f=ReloadBall(f);ItemHandler.UseItem(f.Inventory.Objects.Single(),f);PacketHandler.HandlePacket(frame,f);
            Check(!Fenced(f) && FurnitureCount(f)==1 && HasAchievement(f,10101),"placement retries after production reload: "+fault);
        }
        var expired=FurnitureOwner(town);var pending=Pack(expired,721177,1);var now=new DateTime(2032,1,1,0,0,0,DateTimeKind.Utc);
        Furniture.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try{ItemHandler.UseItem(pending,expired);now=now.AddMinutes(2);PacketHandler.HandlePacket(FurniturePosition(expired,pending.ID),expired);
            Check(!Fenced(expired) && FurnitureCount(expired)==0 && expired.Inventory.Count==1,"furniture selection expires without consuming the item");}
        finally{Furniture.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
        foreach(string fault in new[]{"items","entities","revora_furniture_purchases"}){
            var f=HousePlayer(town);AdventureNear(f,30161);FurnitureNpc(f,10);ulong before=f.Entity.Money;uint beforeCp=f.Entity.ConquerPoints;
            string verb=fault=="entities"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_furniture_buy() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_furniture_buy BEFORE "+verb+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_furniture_buy()");
            try{FurnitureNpc(f,40);}finally{Query("DROP TRIGGER suppress_furniture_buy ON "+fault+";DROP FUNCTION suppress_furniture_buy()");}
            Check(Fenced(f) && f.Inventory.Count==0 && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==(long)before
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",f.Entity.UID)==beforeCp
                && Scalar("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid",f.Entity.UID)==0,
                "failed furniture purchase preserves CPs and silver and grants nothing: "+fault);
            f=ReloadBall(f);AdventureNear(f,30161);FurnitureNpc(f,10);FurnitureNpc(f,40);
            Check(!Fenced(f) && f.Inventory.Count==1 && f.Entity.Money==before && f.Entity.ConquerPoints==beforeCp-2500,"furniture CP purchase retries after reload: "+fault);
        }
        foreach(string fault in new[]{"furniture","revora_house_furniture"}){
            var f=FurnitureOwner(town);var item=Pack(f,721177,1);ItemHandler.UseItem(item,f);PacketHandler.HandlePacket(FurniturePosition(f,item.ID),f);
            uint actor=f.Map.Npcs.Values.Single(n=>n.Type==Enums.NpcType.Furniture).UID;
            string verb=fault=="furniture"?"DELETE":"UPDATE";
            Query("CREATE FUNCTION suppress_furniture_remove() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_furniture_remove BEFORE "+verb+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_furniture_remove()");
            try{RemoveFurniture(f,actor);}finally{Query("DROP TRIGGER suppress_furniture_remove ON "+fault+";DROP FUNCTION suppress_furniture_remove()");}
            Check(FurnitureCount(f)==1 && f.Map.Npcs.ContainsKey(actor) && Scalar("SELECT count(*) FROM furniture WHERE id=@uid",actor)==1,
                "failed furniture removal retains both durable records and the actor: "+fault);
            f=ReloadBall(f);RemoveFurniture(f,actor);
            Check(!Fenced(f) && FurnitureCount(f)==0 && !f.Map.Npcs.ContainsKey(actor),"furniture removal retries after reload: "+fault);
        }
        var guarded=FurnitureOwner(town);var one=Pack(guarded,721177,1);var two=Pack(guarded,721178,1);
        ItemHandler.UseItem(one,guarded);var firstRequest=FurniturePosition(guarded,one.ID);ItemHandler.UseItem(two,guarded);
        PacketHandler.HandlePacket(firstRequest,guarded);
        Check(FurnitureCount(guarded)==0 && guarded.Inventory.Count==2,"old-model request cannot consume newly selected furniture");
        guarded.Trade.InTrade=true;PacketHandler.HandlePacket(FurniturePosition(guarded,two.ID),guarded);guarded.Trade.InTrade=false;
        Check(FurnitureCount(guarded)==0 && guarded.Inventory.Count==2,"trading blocks placement without consuming selection");
        ItemHandler.UseItem(two,guarded);two.Bound=true;
        PacketHandler.HandlePacket(FurniturePosition(guarded,two.ID),guarded);
        Check(FurnitureCount(guarded)==0 && guarded.Inventory.Count==2,"changed binding invalidates furniture selection");
        ItemHandler.UseItem(one,guarded);
        PacketHandler.HandlePacket(FurnitureFrame(2030,4,guarded.Entity.X,5,guarded.Entity.Y,6,10),guarded);
        var exit=guarded.Map.Portals.First();PacketHandler.HandlePacket(FurnitureFrame(2030,4,exit.CurrentX,5,exit.CurrentY,6,10),guarded);
        Check(FurnitureCount(guarded)==0 && guarded.Inventory.Count==2,"player and exit cells cannot be occupied by furniture");
        foreach(int level in new[]{3,4,5}){
            var f=HousePlayer(town);for(int step=1;step<=level;step++){BuyHouseCert(f,step);HouseNpc(f,step==1?(byte)1:(byte)3);HouseNpc(f,40);}HouseNpc(f,2);
            int capacity=level==3?9:level==4?10:12;
            for(int i=0;i<=capacity;i++){var item=Pack(f,721177,1);ItemHandler.UseItem(item,f);PacketHandler.HandlePacket(FurniturePosition(f,item.ID),f);}
            Check(!Fenced(f) && FurnitureCount(f)==capacity && f.Inventory.Count==1,"higher house capacity preserves excess furnishing: level "+level);
        }
    }
    static void BoxOpen(GameState p,uint actor)
    {
        AdventureNear(p,actor);PacketHandler.HandlePacket(FurnitureFrame(2031,1,0,2,actor,3,0,4,0),p);
    }
    static void BoxMove(GameState p,byte action,uint item,uint actor,byte kind=20)
    {
        var frame=StorageFrame(action,item,actor);frame[13]=kind;Warehouse.Process(frame,p);
    }
    static uint PlaceBox(GameState p,ConquerItem item)
    {
        var before=p.Map.Npcs.Keys.ToArray();ItemHandler.UseItem(item,p);var frame=FurniturePosition(p,721189);
        Parallel.For(0,8,i=>PacketHandler.HandlePacket(frame,p));
        return p.Map.Npcs.Keys.Except(before).Single();
    }
    static void VerifyHouseBoxes(Map town)
    {
        var p=StoragePlayer(town);AdventureNear(p,30161);ulong purse=p.Entity.Money;
        for(int n=0;n<3;n++){FurnitureNpc(p,22);Parallel.For(0,8,i=>FurnitureNpc(p,40));}
        Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==721189)==3 && p.Entity.Money==purse-300000
            && Scalar("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid AND item_type=721189",p.Entity.UID)==3,
            "ordinary item-box purchases charge once and persist three exact items");
        p=ReloadBall(p);AdventureNear(p,30156);BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);HouseNpc(p,2);
        var boxes=p.Inventory.Objects.Where(i=>i.ID==721189).ToArray();uint first=PlaceBox(p,boxes[0]);
        Check(!Fenced(p) && (byte)p.Map.Npcs[first].Type==4 && p.Map.Npcs[first].Mesh==8200 && FurnitureCount(p)==1 && !HasAchievement(p,10101),
            "level-one home places one paid regular-NPC box without inventing decorative achievement credit");
        ItemHandler.UseItem(boxes[1],p);PacketHandler.HandlePacket(FurniturePosition(p,721189),p);
        Check(FurnitureCount(p)==1 && p.Inventory.ContainsUID(boxes[1].UID),"level-one second box is refused without consuming its item");
        RemoveFurniture(p,first);Check(FurnitureCount(p)==0 && !p.Warehouses.ContainsKey((Revora.Game.ConquerStructures.Warehouse.WarehouseID)first)
            && p.Inventory.Objects.Count(i=>i.ID==721189)==2,"empty box removal is durable and destructive with no refund");
        p.Entity.Teleport(1002,345,529);AdventureNear(p,30156);BuyHouseCert(p,2);HouseNpc(p,3);HouseNpc(p,40);HouseNpc(p,2);
        first=PlaceBox(p,boxes[1]);uint second=PlaceBox(p,boxes[2]);
        var excess=Pack(p,721189,1);ItemHandler.UseItem(excess,p);PacketHandler.HandlePacket(FurniturePosition(p,721189),p);
        Check(FurnitureCount(p)==2 && p.Inventory.ContainsUID(excess.UID),"level-two home keeps two separate boxes and refuses a third");
        var decoration=Pack(p,721177,1);ItemHandler.UseItem(decoration,p);PacketHandler.HandlePacket(FurniturePosition(p,721177),p);
        Check(FurnitureCount(p)==3 && HasAchievement(p,10101),"box capacity remains separate from decorative furniture and its achievement");
        var a=Pack(p,1088001,7);var b=Pack(p,1088001,3);BoxOpen(p,first);Parallel.For(0,8,i=>BoxMove(p,1,a.UID,first));
        BoxOpen(p,second);Parallel.For(0,8,i=>BoxMove(p,1,b.UID,second));
        Check(!Fenced(p) && a.Warehouse==first && b.Warehouse==second && Scalar("SELECT warehouse FROM items WHERE uid=@uid",a.UID)==first
            && Scalar("SELECT warehouse FROM items WHERE uid=@uid",b.UID)==second,"two purchased placed boxes retain separate exact stacks under concurrent requests");
        BoxMove(p,2,a.UID,second);RemoveFurniture(p,first);
        Check(a.Warehouse==first && FurnitureCount(p)==3 && p.Map.Npcs.ContainsKey(first),"wrong-box withdrawal and nonempty removal preserve the original box and contents");
        var stranger=StoragePlayer(town);BoxMove(stranger,2,a.UID,first);RemoveFurniture(stranger,first);
        Check(!stranger.Inventory.ContainsUID(a.UID) && p.Map.Npcs.ContainsKey(first),"another account cannot withdraw from or remove a private box");
        BoxOpen(p,first);BoxMove(p,2,a.UID,first,10);
        Check(a.Warehouse==first && !p.Inventory.ContainsUID(a.UID),"house-box requests require the audited storage kind");
        p.WarehousePW=12345;p.WarehouseOpen=false;BoxMove(p,2,a.UID,first);
        Check(a.Warehouse==first,"locked secondary password blocks box withdrawals");p.WarehousePW=0;
        p.Entity.Teleport(1002,345,529);BoxMove(p,2,a.UID,first);
        Check(a.Warehouse==first && !p.Inventory.ContainsUID(a.UID),"leaving the private home revokes box access");
        ushort home=p.Map.ID;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT map_id FROM revora_house_maps WHERE uid=@uid",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);home=Convert.ToUInt16(q.ExecuteScalar());}}
        Kernel.Maps[home].Dispose();MapsTable.MapInformations.Remove(home);
        Query("UPDATE entities SET mapid=@map WHERE uid=@uid","map",(int)home,"uid",(long)p.Entity.UID);p=ReloadBall(p);
        Check(!Fenced(p) && p.Entity.MapID==home && p.Map.Npcs.ContainsKey(first) && p.Map.Npcs.ContainsKey(second)
            && p.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)first].GetItem(a.UID).StackSize==7
            && p.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)second].GetItem(b.UID).StackSize==3,
            "cold map and production item reload reconstruct both owned boxes and separate contents");
        BoxOpen(p,first);BoxMove(p,2,a.UID,first);RemoveFurniture(p,first);RemoveFurniture(p,first);
        Check(!Fenced(p) && p.Inventory.Objects.Single(i=>i.UID==a.UID).StackSize==7 && !p.Map.Npcs.ContainsKey(first)
            && Scalar("SELECT count(*) FROM revora_house_furniture WHERE uid=@uid AND item_type=721189 AND removed_at IS NULL",p.Entity.UID)==1,
            "reloaded box withdraws its whole stack and empty removal cannot replay a refund");
        BoxOpen(p,second);BoxMove(p,2,b.UID,second);
        Query("CREATE FUNCTION suppress_box_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_box_item BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_box_item()");
        try{BoxMove(p,1,a.UID,second);}finally{Query("DROP TRIGGER suppress_box_item ON items;DROP FUNCTION suppress_box_item()");}
        Check(Fenced(p) && p.Inventory.ContainsUID(a.UID) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",a.UID)==0,
            "failed box transfer retains saved and runtime inventory and requires reload");
        p=ReloadBall(p);BoxOpen(p,second);BoxMove(p,1,a.UID,second);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(a.UID) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",a.UID)==second,
            "ordinary reconnect retries a failed box transfer once");
        Query("UPDATE revora_house_furniture SET removed_at=now() WHERE actor=@actor","actor",(long)second);
        var reload=ReloadGold(p);bool failed=false;try{ConquerItemTable.LoadItems(reload);}catch(InvalidOperationException){failed=true;}
        Check(failed && Fenced(reload) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",a.UID)==second,
            "orphaned box contents stop login projection instead of silently losing stored items");
        VerifyBoxFailures(town);
    }
    static void VerifyBoxFailures(Map town)
    {
        var legacy=StoragePlayer(town);RegisterStorage(legacy,100);var legacyItem=Pack(legacy,1088001,2);
        Warehouse.Process(StorageFrame(1,legacyItem.UID,100),legacy);
        Check(!Fenced(legacy) && legacy.Inventory.ContainsUID(legacyItem.UID) && legacyItem.Warehouse==0,
            "unearned legacy house bucket cannot bypass paid box placement");
        legacy.Inventory.Remove(legacyItem.UID,Enums.ItemUse.None,false);legacyItem.Warehouse=100;
        Query("UPDATE items SET warehouse=100 WHERE uid=@uid","uid",(long)legacyItem.UID);
        legacy.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.House].Add(legacyItem);
        Warehouse.Process(StorageFrame(2,legacyItem.UID,100),legacy);
        Check(!Fenced(legacy) && legacy.Inventory.ContainsUID(legacyItem.UID) && legacyItem.Warehouse==0
            && Scalar("SELECT warehouse FROM items WHERE uid=@uid",legacyItem.UID)==0,"historical house-bucket items remain withdrawable without reassignment or deletion");
        var full=ReloadBall(FurnitureOwner(town));uint box=PlaceBox(full,Pack(full,721189,1));BoxOpen(full,box);
        for(int i=0;i<60;i++){var item=Pack(full,1088001,1);BoxMove(full,1,item.UID,box);}
        var excess=Pack(full,1088001,1);BoxMove(full,1,excess.UID,box);RemoveFurniture(full,box);
        Check(!Fenced(full) && full.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)box].Count==60
            && full.Inventory.ContainsUID(excess.UID) && full.Map.Npcs.ContainsKey(box)
            && Scalar("SELECT count(*) FROM items WHERE warehouse=@uid",box)==60,"box has sixty durable slots and refuses overflow or full-box removal");
        var racer=ReloadBall(FurnitureOwner(town));uint contested=PlaceBox(racer,Pack(racer,721189,1));BoxOpen(racer,contested);
        var stake=Pack(racer,1088001,2);Parallel.Invoke(()=>BoxMove(racer,1,stake.UID,contested),()=>RemoveFurniture(racer,contested));
        bool present=racer.Map.Npcs.ContainsKey(contested);
        Check(!Fenced(racer) && stake.Warehouse==(present?contested:0) && racer.Inventory.ContainsUID(stake.UID)!=present
            && Scalar("SELECT warehouse FROM items WHERE uid=@uid",stake.UID)==(present?contested:0)
            && FurnitureCount(racer)==(present?1:0),"deposit versus removal race commits one consistent box/item outcome");
        foreach(string fault in new[]{"items","furniture","revora_house_furniture"}){
            var f=ReloadBall(FurnitureOwner(town));var item=Pack(f,721189,1);ItemHandler.UseItem(item,f);var frame=FurniturePosition(f,721189);
            string verb=fault=="items"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_box_place() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_box_place BEFORE "+verb+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_box_place()");
            try{PacketHandler.HandlePacket(frame,f);}finally{Query("DROP TRIGGER suppress_box_place ON "+fault+";DROP FUNCTION suppress_box_place()");}
            Check(Fenced(f) && f.Inventory.ContainsUID(item.UID) && FurnitureCount(f)==0 && !f.Map.Npcs.Values.Any(n=>(byte)n.Type==4),
                "failed box placement preserves its item and creates no actor/storage: "+fault);
            f=ReloadBall(f);uint actor=PlaceBox(f,f.Inventory.Objects.Single(i=>i.UID==item.UID));
            Check(!Fenced(f) && f.Warehouses.ContainsKey((Revora.Game.ConquerStructures.Warehouse.WarehouseID)actor)
                && FurnitureCount(f)==1 && !f.Inventory.ContainsUID(item.UID),"failed placement retries as one owned box after reload: "+fault);
        }
        var removal=ReloadBall(FurnitureOwner(town));uint empty=PlaceBox(removal,Pack(removal,721189,1));
        Query("CREATE FUNCTION suppress_box_remove() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_box_remove BEFORE DELETE ON furniture FOR EACH ROW EXECUTE FUNCTION suppress_box_remove()");
        try{RemoveFurniture(removal,empty);}finally{Query("DROP TRIGGER suppress_box_remove ON furniture;DROP FUNCTION suppress_box_remove()");}
        Check(Fenced(removal) && removal.Map.Npcs.ContainsKey(empty) && FurnitureCount(removal)==1,"failed box removal rolls back the owned record and retains the actor");
        removal=ReloadBall(removal);RemoveFurniture(removal,empty);
        Check(!Fenced(removal) && !removal.Map.Npcs.ContainsKey(empty) && FurnitureCount(removal)==0,"failed empty-box removal retries after ordinary reload");
        var corrupt=ReloadBall(FurnitureOwner(town));uint badBox=PlaceBox(corrupt,Pack(corrupt,721189,1));BoxOpen(corrupt,badBox);
        var owned=Pack(corrupt,1088001,1);BoxMove(corrupt,1,owned.UID,badBox);
        Query("UPDATE items SET entityid=@other WHERE uid=@uid","other",(long)full.Entity.UID,"uid",(long)owned.UID);
        var returning=ReloadGold(corrupt);bool rejected=false;try{ConquerItemTable.LoadItems(returning);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Fenced(returning) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",owned.UID)==badBox,
            "foreign-owned contents require reconciliation rather than a silent partial box load");
    }
    static void VerifyFurnitureFootprints()
    {
        var map=(Map)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Map));
        map.Floor=new Floor(16,16,0);map.Npcs=new Dictionary<uint,Revora.Interfaces.INpc>();
        var shelf=new NpcSpawn{UID=1810000001,MapID=0,X=8,Y=8,Mesh=20,Type=Enums.NpcType.Furniture};
        map.AddNpc(shelf);
        Check(!map.Floor[8,8,MapObjectType.Player] && !map.Floor[7,8,MapObjectType.Player] && !map.Floor[6,8,MapObjectType.Player]
            && !map.Floor[8,7,MapObjectType.Player] && map.Floor[7,7,MapObjectType.Player] && map.Floor[6,7,MapObjectType.Player]
            && map.Floor[9,8,MapObjectType.Player],"shelf footprint uses native negative-axis origin and selective access mask");
        map.Floor[7,8,MapObjectType.InvalidCast]=false;
        var overlap=new NpcSpawn{UID=1810000002,MapID=0,X=6,Y=8,Mesh=10,Type=Enums.NpcType.Furniture};map.AddNpc(overlap);
        map.RemoveNpc(shelf);
        Check(map.Floor[8,8,MapObjectType.Player] && !map.Floor[7,8,MapObjectType.Player] && !map.Floor[6,8,MapObjectType.Player],
            "furniture removal preserves independent terrain and overlapping furniture blockers");
        map.RemoveNpc(overlap);Check(map.Floor[6,8,MapObjectType.Player] && !map.Floor[7,8,MapObjectType.Player],"last furniture blocker clears without opening terrain");
        var tall=new NpcSpawn{UID=1810000003,MapID=0,X=10,Y=10,Mesh=40,Type=Enums.NpcType.Furniture};map.AddNpc(tall);
        Check(map.Floor[10,10,MapObjectType.Player] && map.Floor[9,10,MapObjectType.Player],"zero access flags are not converted into guessed rectangle blockers");map.RemoveNpc(tall);
        for(ushort dir=0;dir<8;dir++){
            var screen=new NpcSpawn{UID=(uint)(1810000010+dir),MapID=0,X=12,Y=12,Mesh=(ushort)(80+dir),Type=Enums.NpcType.Furniture};map.AddNpc(screen);
            Check(!map.Floor[12,12,MapObjectType.Player] && !map.Floor[12,9,MapObjectType.Player] && map.Floor[12,8,MapObjectType.Player],"shared scene has the same screen access mask for direction "+dir);
            map.RemoveNpc(screen);
        }
    }
}
